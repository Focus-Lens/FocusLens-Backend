using System.Text.Json;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Contracts.StudySessions;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.StudySessions;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.StudySessions;

public sealed class StudySessionAiHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudySession> studySessionRepository,
    IBaseRepository<StudyMaterialSection> sectionRepository,
    IBaseRepository<StudySessionSelectedSection> selectedSectionRepository,
    IBaseRepository<StudySessionQuestion> questionRepository,
    IBaseRepository<StudySessionQuestionAnswer> questionAnswerRepository,
    IStudyMaterialFileStore fileStore,
    IFocusLensAiClient aiClient,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<AnalyzeStudySessionContentCommand, Result<StudySessionContentAnalysisResponse>>,
      IRequestHandler<SelectStudySessionAiSectionsCommand, Result<StudySessionResponse>>
{
    public async Task<Result<StudySessionContentAnalysisResponse>> Handle(
        AnalyzeStudySessionContentCommand request,
        CancellationToken cancellationToken)
    {
        StudySession? session = await GetOwnedSessionAsync(request.SessionId);

        if (session is null)
        {
            return Error.NotFound(
                "StudySessions.NotFound",
                "The study session was not found.");
        }

        if (session.Material is null)
        {
            return StudySessionErrors.MaterialRequired;
        }

        if (session.Selection?.DerivedStorageReference is not { Length: > 0 } derivedReference)
        {
            return Error.Validation(
                "StudySessionAi.SelectionRequired",
                "A page selection must be created before AI analysis.");
        }

        await using Stream derivedStream =
            await fileStore.OpenReadAsync(derivedReference, cancellationToken);

        using MemoryStream buffer = new();
        await derivedStream.CopyToAsync(buffer, cancellationToken);

        AiContentProcessingResult result =
            await aiClient.ProcessFileAsync(
                buffer.ToArray(),
                session.Material.FileName,
                "application/pdf",
                cancellationToken);

        if (result.Content is null || result.Content.Sections.Count == 0)
        {
            return Error.Validation(
                "StudySessionAi.AnalysisEmpty",
                "AI analysis did not return any sections.");
        }

        if (string.IsNullOrWhiteSpace(result.ExtractedText))
        {
            return Error.Validation(
                "StudySessionAi.ExtractedTextUnavailable",
                "AI analysis did not return extracted text.");
        }

        Result<Success> saveAnalysisResult =
            session.Selection!.SetAiAnalysis(
                result.ExtractedText,
                JsonSerializer.Serialize(result.Content));

        if (saveAnalysisResult.IsError)
        {
            return saveAnalysisResult.TopError;
        }

        await unitOfWork.SaveChangesAsync();

        return new StudySessionContentAnalysisResponse(
            result.Content.Sections
                .Select(section =>
                    new StudySessionAiSectionResponse(
                        section.SectionId,
                        section.Title,
                        section.Concepts
                            .Select(concept =>
                                new StudySessionAiConceptResponse(
                                    concept.ConceptId,
                                    concept.Name,
                                    concept.KeyLearningPoints,
                                    concept.Difficulty,
                                    concept.EstimatedTimeMinutes))
                            .ToArray(),
                        section.Difficulty,
                        section.EstimatedTimeMinutes,
                        section.FromPage,
                        section.ToPage))
                .ToArray());
    }

    public async Task<Result<StudySessionResponse>> Handle(
        SelectStudySessionAiSectionsCommand request,
        CancellationToken cancellationToken)
    {
        StudySession? session = await GetOwnedSessionAsync(request.SessionId);

        if (session is null)
        {
            return Error.NotFound(
                "StudySessions.NotFound",
                "The study session was not found.");
        }

        if (session.Selection?.AiAnalysisJson is not { Length: > 0 } analysisJson)
        {
            return Error.Validation(
                "StudySessionAi.AnalysisRequired",
                "Analyze the selected material before selecting sections.");
        }

        if (session.Selection.AiExtractedText is not { Length: > 0 } extractedText)
        {
            return Error.Validation(
                "StudySessionAi.ExtractedTextUnavailable",
                "AI extracted text is unavailable.");
        }

        string[] requestedIds = request.Request.SectionIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray()
            ?? [];

        if (requestedIds.Length == 0)
        {
            return Error.Validation(
                "StudySessionAi.SectionsRequired",
                "At least one AI section must be selected.");
        }

        AiContentAnalysisResult? analysis =
            JsonSerializer.Deserialize<AiContentAnalysisResult>(
                analysisJson,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

        if (analysis is null || analysis.Sections.Count == 0)
        {
            return Error.Validation(
                "StudySessionAi.AnalysisInvalid",
                "Stored AI analysis is invalid or empty.");
        }

        List<AiContentSection> selectedAiSections = [];

        foreach (string sectionId in requestedIds)
        {
            AiContentSection? section =
                analysis.Sections.FirstOrDefault(item =>
                    string.Equals(
                        item.SectionId,
                        sectionId,
                        StringComparison.Ordinal));

            if (section is null)
            {
                return Error.Validation(
                    "StudySessionAi.SectionNotFound",
                    $"AI section {sectionId} was not found.");
            }

            if (section.Concepts.Count == 0)
            {
                return Error.Validation(
                    "StudySessionAi.SectionHasNoConcept",
                    $"AI section {section.Title} has no concepts.");
            }

            if (section.FromPage < 1 || section.ToPage < section.FromPage)
            {
                return Error.Validation(
                    "StudySessionAi.SectionPageRangeInvalid",
                    $"AI section {section.Title} has an invalid page range.");
            }

            int derivedPageCount = session.Selection.ToPage - session.Selection.FromPage + 1;
            if (section.ToPage > derivedPageCount)
            {
                return Error.Validation(
                    "StudySessionAi.SectionPageRangeOutsideSelection",
                    $"AI section {section.Title} extends outside the selected PDF pages.");
            }

            selectedAiSections.Add(section);
        }

        IEnumerable<StudySessionSelectedSection> oldSelectedSections =
            await selectedSectionRepository.GetAllAsync(item =>
                item.StudySessionSelectionId == session.Selection.Id);

        selectedSectionRepository.DeleteRange(oldSelectedSections);

        Guid[] oldMaterialSectionIds = oldSelectedSections
            .Select(section => section.StudyMaterialSectionId)
            .ToArray();

        List<StudySessionQuestion> oldQuestions =
            (await questionRepository.GetAllAsync(item =>
                item.StudySessionId == session.Id))
            .ToList();

        if (oldQuestions.Count > 0)
        {
            Guid[] questionIds =
                oldQuestions.Select(item => item.Id).ToArray();

            IEnumerable<StudySessionQuestionAnswer> oldAnswers =
                await questionAnswerRepository.GetAllAsync(item =>
                    questionIds.Contains(item.StudySessionQuestionId));

            questionAnswerRepository.DeleteRange(oldAnswers);
            questionRepository.DeleteRange(oldQuestions);
        }

        if (oldMaterialSectionIds.Length > 0)
        {
            IEnumerable<StudyMaterialSection> oldMaterialSections =
                await sectionRepository.GetAllAsync(section =>
                    oldMaterialSectionIds.Contains(section.Id));
            sectionRepository.DeleteRange(oldMaterialSections);
        }

        List<StudyMaterialSection> materialSections = [];

        foreach (AiContentSection aiSection in selectedAiSections)
        {
            Result<StudyMaterialSection> sectionResult =
                StudyMaterialSection.Create(
                    session.StudyMaterialId!.Value,
                    aiSection.Title,
                    Math.Max(1, aiSection.EstimatedTimeMinutes),
                    session.Selection.FromPage + aiSection.FromPage - 1,
                    session.Selection.FromPage + aiSection.ToPage - 1);

            if (sectionResult.IsError)
            {
                return sectionResult.TopError;
            }

            materialSections.Add(sectionResult.Value);
        }

        Result<Success> setSectionsResult =
            session.SetSelectedSections(materialSections);

        if (setSectionsResult.IsError)
        {
            return setSectionsResult.TopError;
        }

        Result<Success> setAiIdsResult =
            session.Selection.SetSelectedAiSectionIds(requestedIds);

        if (setAiIdsResult.IsError)
        {
            return setAiIdsResult.TopError;
        }

        await sectionRepository.AddRangeAsync(materialSections);
        await selectedSectionRepository.AddRangeAsync(
            session.Selection.SelectedSections);

        List<StudySessionQuestion> questions = [];

        for (int index = 0; index < selectedAiSections.Count; index++)
        {
            AiContentSection aiSection = selectedAiSections[index];
            AiContentConcept aiConcept = aiSection.Concepts.First();

            AiMcqGenerationResult mcq =
                await aiClient.GenerateMcqAsync(
                    aiSection.SectionId,
                    aiSection.Title,
                    aiConcept.ConceptId,
                    aiConcept.Name,
                    extractedText,
                    1,
                    cancellationToken);

            AiMcqQuestion? generated = mcq.Questions.FirstOrDefault();

            if (generated is null)
            {
                return Error.Validation(
                    "StudySessionAi.QuestionGenerationFailed",
                    $"Could not generate a mandatory challenge for {aiSection.Title}.");
            }

            Result<StudySessionQuestion> questionResult =
                StudySessionQuestion.Create(
                    session.Id,
                    materialSections[index].Id,
                    aiSection.SectionId,
                    aiSection.Title,
                    aiConcept.ConceptId,
                    aiConcept.Name,
                    generated.Question,
                    generated.Options,
                    generated.CorrectAnswer,
                    generated.Difficulty,
                    generated.Explanation,
                    Math.Max(1, generated.EstimatedTimeMinutes),
                    index + 1);

            if (questionResult.IsError)
            {
                return questionResult.TopError;
            }

            questions.Add(questionResult.Value);
        }

        await questionRepository.AddRangeAsync(questions);
        await unitOfWork.SaveChangesAsync();

        return session.ToResponse(timeProvider.GetUtcNow());
    }

    private async Task<StudySession?> GetOwnedSessionAsync(Guid sessionId)
    {
        if (sessionId == Guid.Empty ||
            currentUser.UserId is not Guid userId ||
            userId == Guid.Empty)
        {
            return null;
        }

        Student? student =
            await studentRepository.FirstOrDefaultAsync(
                item => item.UserId == userId);

        if (student is null)
        {
            return null;
        }

        return await studySessionRepository.FirstOrDefaultAsync(
            item => item.Id == sessionId &&
                    item.StudentId == student.Id,
            item => item.Material!,
            item => item.Selection!,
            item => item.CompletedSections);
    }
}
