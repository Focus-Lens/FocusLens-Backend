using FocusLens.Domain.Common.Results;

namespace FocusLens.Domain.StudySessions;

public static class StudySessionErrors
{
    public static readonly Error StudentIdRequired = Error.Validation("StudySessions.StudentIdRequired", "Student id is required.");
    public static readonly Error InvalidMode = Error.Validation("StudySessions.InvalidMode", "Study session mode is invalid.");
    public static readonly Error SubjectRequired = Error.Validation("StudySessions.SubjectRequired", "A subject is required.");
    public static readonly Error DurationInvalid = Error.Validation("StudySessions.DurationInvalid", "Focus duration must be greater than zero.");
    public static readonly Error MaterialRequired = Error.Validation("StudySessions.MaterialRequired", "A study material is required before a session can be ready.");
    public static readonly Error MaterialMismatch = Error.Validation("StudySessions.MaterialMismatch", "The material does not belong to this student or session.");
    public static readonly Error PageRangeRequiresMaterial = Error.Validation("StudySessions.PageRangeRequiresMaterial", "Attach material before selecting a page range.");
    public static readonly Error PageRangeExceedsMaterial = Error.Validation("StudySessions.PageRangeExceedsMaterial", "The selected page range exceeds the material page count.");
    public static readonly Error SectionsRequireMaterial = Error.Validation("StudySessions.SectionsRequireMaterial", "Attach material before selecting sections.");
    public static readonly Error SectionMismatch = Error.Validation("StudySessions.SectionMismatch", "Each selected section must belong to the attached material.");
    public static readonly Error DuplicateSection = Error.Validation("StudySessions.DuplicateSection", "A section can only be selected once.");
    public static readonly Error NotReady = Error.Conflict("StudySessions.NotReady", "Complete the study session setup before starting.");
    public static readonly Error NotConfigurable = Error.Conflict("StudySessions.NotConfigurable", "Only draft or ready study sessions can be configured.");
}
