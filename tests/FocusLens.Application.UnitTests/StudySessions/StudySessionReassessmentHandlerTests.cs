using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.StudySessions;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Students;
using FocusLens.Domain.StudySessions;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace FocusLens.Application.UnitTests.StudySessions;

public sealed class StudySessionReassessmentHandlerTests
{
    [Fact]
    public async Task Reassess_BeforeTheScheduledChallenge_IsRejected()
    {
        Fixture fixture = Fixture.Create(challengeAvailable: false, reassessmentEligible: true);

        var result = await fixture.Handler.Handle(
            new MakeStudySessionReassessmentAvailableCommand(fixture.Session.Id), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("StudySessionQuestions.ChallengeUnavailable", result.TopError.Code);
        Assert.Single(fixture.Questions.GetAll());
    }

    [Fact]
    public async Task Reassess_AfterRetriesAndExplanation_CreatesAndReturnsTheReassessment()
    {
        Fixture fixture = Fixture.Create(challengeAvailable: true, reassessmentEligible: true);

        var result = await fixture.Handler.Handle(
            new MakeStudySessionReassessmentAvailableCommand(fixture.Session.Id), CancellationToken.None);
        var current = await fixture.Handler.Handle(
            new GetCurrentStudySessionQuestionQuery(fixture.Session.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        StudySessionQuestion reassessment = Assert.Single(fixture.Questions.GetAll().Where(question => question.IsReassessment));
        Assert.NotNull(current?.Question);
        Assert.Equal(reassessment.Id, current.Question!.Id);
    }

    [Fact]
    public async Task CorrectReassessment_CompletesTheSectionWithoutRequiringTheOriginalAgain()
    {
        Fixture fixture = Fixture.Create(challengeAvailable: true, reassessmentEligible: true);
        Assert.True((await fixture.Handler.Handle(
            new MakeStudySessionReassessmentAvailableCommand(fixture.Session.Id), CancellationToken.None)).IsSuccess);

        StudySessionQuestion reassessment = Assert.Single(fixture.Questions.GetAll().Where(question => question.IsReassessment));
        var answer = await fixture.Handler.Handle(new SubmitStudySessionQuestionAnswerCommand(
            fixture.Session.Id, reassessment.Id, new("A")), CancellationToken.None);
        var current = await fixture.Handler.Handle(
            new GetCurrentStudySessionQuestionQuery(fixture.Session.Id), CancellationToken.None);

        Assert.True(answer.IsSuccess);
        Assert.True(answer.Value.SessionCompleted);
        Assert.Equal(StudySessionStatus.Completed, fixture.Session.Status);
        Assert.Null(current);
    }

    [Fact]
    public async Task CorrectFirstIncreasedFrequencyQuestion_CreatesSecondQuestionWithoutCompletingSection()
    {
        Fixture fixture = Fixture.Create(
            challengeAvailable: true,
            reassessmentEligible: false,
            increasedAssessmentFrequency: true);
        StudySessionQuestion original = Assert.Single(fixture.Questions.GetAll());

        var answer = await fixture.Handler.Handle(new SubmitStudySessionQuestionAnswerCommand(
            fixture.Session.Id, original.Id, new("A")), CancellationToken.None);
        var current = await fixture.Handler.Handle(
            new GetCurrentStudySessionQuestionQuery(fixture.Session.Id), CancellationToken.None);

        Assert.True(answer.IsSuccess);
        Assert.False(answer.Value.SessionCompleted);
        Assert.Equal(StudySessionStatus.Active, fixture.Session.Status);
        Assert.Equal(2, fixture.Questions.GetAll().Count());
        Assert.Null(current?.Question);
        Assert.False(fixture.SelectedSection.IsChallengeAvailable);

        var beforeBoundary = await fixture.ProgressHandler.Handle(
            new UpdateStudySessionProgressCommand(
                fixture.Session.Id,
                new(3)),
            CancellationToken.None);
        bool challengeBeforeBoundary = fixture.SelectedSection.IsChallengeAvailable;
        var atBoundary = await fixture.ProgressHandler.Handle(
            new UpdateStudySessionProgressCommand(
                fixture.Session.Id,
                new(4)),
            CancellationToken.None);
        var boundaryQuestion = await fixture.Handler.Handle(
            new GetCurrentStudySessionQuestionQuery(fixture.Session.Id),
            CancellationToken.None);

        Assert.True(beforeBoundary.IsSuccess);
        Assert.False(challengeBeforeBoundary);
        Assert.True(atBoundary.IsSuccess);
        Assert.True(fixture.SelectedSection.IsChallengeAvailable);
        Assert.NotNull(boundaryQuestion?.Question);
        Assert.NotEqual(original.Id, boundaryQuestion.Question!.Id);
    }

    private sealed record Fixture(
        StudySessionQuestionHandler Handler,
        StudySessionCommandHandler ProgressHandler,
        StudySession Session,
        InMemoryRepository<StudySessionQuestion> Questions,
        StudySessionSelectedSection SelectedSection)
    {
        public static Fixture Create(
            bool challengeAvailable,
            bool reassessmentEligible,
            bool increasedAssessmentFrequency = false)
        {
            Guid userId = Guid.NewGuid();
            Student student = new(userId);
            StudentSubject subject = StudentSubject.Predefined(StudentSubjectType.Math);
            student.ReplaceSubjects([subject]);
            StudySession session = StudySession.Create(student.Id, StudySessionMode.Digital).Value;
            StudyMaterial material = StudyMaterial.Create(student.Id, "lesson.pdf", 4, 4,
                "materials/lesson.pdf", StudyMaterialSource.Upload).Value;
            StudyMaterialSection materialSection = StudyMaterialSection.Create(material.Id, "Cells", 8, 1, 4).Value;

            Assert.True(session.SetSubject(subject).IsSuccess);
            Assert.True(session.SetDuration(25).IsSuccess);
            Assert.True(session.SetStudyMaterial(material).IsSuccess);
            StudySessionSelection selection = StudySessionSelection.Create(session, material, 1, 4).Value;
            Assert.True(selection.SetAiAnalysis("cell division content", "{}").IsSuccess);
            Assert.True(session.SetSelection(selection).IsSuccess);
            Assert.True(session.SetSelectedSections([materialSection]).IsSuccess);
            Assert.True(session.MarkReady().IsSuccess);

            DateTimeOffset now = DateTimeOffset.UtcNow;
            Assert.True(session.Start(now).IsSuccess);
            if (increasedAssessmentFrequency)
            {
                Assert.True(session.EnableIncreasedAssessmentFrequency().IsSuccess);
            }
            StudySessionSelectedSection selectedSection = selection.SelectedSections.Single();
            // EF assigns this relationship when the selection graph is saved.
            // The in-memory repository used by this focused handler test does
            // not perform relationship fix-up, so mirror that persisted state.
            typeof(StudySessionSelectedSection)
                .GetProperty(nameof(StudySessionSelectedSection.StudySessionSelectionId))!
                .SetValue(selectedSection, selection.Id);
            Assert.True(selectedSection.Start(now).IsSuccess);
            if (challengeAvailable)
            {
                Assert.True(selectedSection.MakeChallengeAvailable(now).IsSuccess);
            }

            StudySessionQuestion original = StudySessionQuestion.Create(session.Id, materialSection.Id,
                "section", "Cells", "concept", "Division", "What happens?",
                ["A", "B", "C", "D"], "A", "easy", "Explanation", 1, 1).Value;
            if (reassessmentEligible)
            {
                Assert.True(original.MarkExplanationShown(now).IsSuccess);
            }

            InMemoryRepository<StudySessionQuestion> questions = new(original);
            InMemoryRepository<StudySessionQuestionAnswer> answers = reassessmentEligible
                ? new InMemoryRepository<StudySessionQuestionAnswer>(
                    StudySessionQuestionAnswer.Create(original.Id, "B", false, null, 1, now).Value,
                    StudySessionQuestionAnswer.Create(original.Id, "C", false, null, 2, now).Value)
                : new InMemoryRepository<StudySessionQuestionAnswer>();

            StudySessionQuestionHandler handler = new(
                new InMemoryRepository<Student>(student),
                new InMemoryRepository<StudySession>(session),
                questions,
                answers,
                new InMemoryRepository<StudySessionSelectedSection>(selectedSection),
                new InMemoryRepository<StudyMaterialSection>(materialSection),
                new FakeCurrentUser(userId),
                new ReassessmentAiClient(),
                new InMemoryRepository<StudySessionBehaviorAnalysisJob>(),
                new FakeUnitOfWork(),
                TimeProvider.System);

            StudySessionCommandHandler progressHandler = new(
                new InMemoryRepository<Student>(student),
                new InMemoryRepository<StudySession>(session),
                new InMemoryRepository<StudySessionPauseInterval>(),
                new InMemoryRepository<StudyMaterial>(material),
                new InMemoryRepository<StudySessionSelection>(selection),
                new InMemoryRepository<StudySessionSelectedSection>(selectedSection),
                new InMemoryRepository<StudyMaterialSection>(materialSection),
                new InMemoryRepository<StudySessionImage>(),
                new TestMaterialFileStore(),
                new TestImageFileStore(),
                new TestPdfProcessor(),
                new FakeCurrentUser(userId),
                new InMemoryRepository<StudySessionBehaviorAnalysisJob>(),
                new FakeUnitOfWork(),
                Options.Create(new StudySessionImageUploadOptions()),
                TimeProvider.System);

            return new Fixture(handler, progressHandler, session, questions, selectedSection);
        }
    }

    private sealed class TestMaterialFileStore : IStudyMaterialFileStore
    {
        public Task<string> SaveOriginalAsync(Guid studentId, string fileName, Stream content,
            CancellationToken cancellationToken) => Task.FromResult("original.pdf");
        public Task<Stream> OpenReadAsync(string storageReference, CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream());
        public Task<string> SaveDerivedAsync(Guid studentId, string fileName, Stream content,
            CancellationToken cancellationToken) => Task.FromResult("derived.pdf");
        public Task DeleteAsync(string storageReference, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class TestImageFileStore : IStudySessionImageFileStore
    {
        public Task<string> SaveAsync(Guid studentId, string extension, Stream content,
            CancellationToken cancellationToken) => Task.FromResult("image");
        public Task DeleteAsync(string storageReference, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task<Stream> OpenReadAsync(string storageReference, CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream());
    }

    private sealed class TestPdfProcessor : IStudyMaterialPdfProcessor
    {
        public Task<int> GetPageCountAsync(Stream content, CancellationToken cancellationToken) =>
            Task.FromResult(4);
        public Task<Stream> ExtractPagesAsync(Stream content, StudySessionPageRange pageRange,
            CancellationToken cancellationToken) => Task.FromResult<Stream>(new MemoryStream());
    }

    private sealed class ReassessmentAiClient : IFocusLensAiClient
    {
        public Task<AiMcqGenerationResult> GenerateMcqAsync(string sectionId, string sectionTitle, string conceptId,
            string conceptName, string content, int numberOfQuestions, CancellationToken cancellationToken) =>
            Task.FromResult(new AiMcqGenerationResult([
                new AiMcqQuestion("Reassessment?", ["A", "B", "C", "D"], "A", sectionId, sectionTitle,
                    conceptId, conceptName, "easy", "Explanation", 1)
            ]));

        public Task<AiAnswerEvaluationResult> EvaluateAnswerAsync(string question, string selectedAnswer,
            string correctAnswer, string sectionId, string conceptId, string content, CancellationToken cancellationToken) =>
            Task.FromResult(new AiAnswerEvaluationResult(true, sectionId, conceptId, "understood"));

        public Task<AiExplanationResult> GenerateExplanationAsync(string sectionId, string sectionTitle,
            string conceptId, string conceptName, string content, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<AiContentProcessingResult> ProcessFileAsync(byte[] content, string fileName, string contentType,
            CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
