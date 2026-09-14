namespace FocusLens.Contracts.StudySessions;

public sealed record StudySessionAiConceptResponse(
    string ConceptId,
    string Name,
    IReadOnlyCollection<string> KeyLearningPoints,
    string Difficulty,
    int EstimatedTimeMinutes);

public sealed record StudySessionAiSectionResponse(
    string SectionId,
    string Title,
    IReadOnlyCollection<StudySessionAiConceptResponse> Concepts,
    string Difficulty,
    int EstimatedTimeMinutes,
    int FromPage,
    int ToPage);

public sealed record StudySessionContentAnalysisResponse(
    IReadOnlyCollection<StudySessionAiSectionResponse> Sections);

public sealed record SelectStudySessionAiSectionsRequest(
    IReadOnlyCollection<string> SectionIds);
