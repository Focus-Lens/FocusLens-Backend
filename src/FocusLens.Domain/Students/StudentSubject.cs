using FocusLens.Domain.Common;

namespace FocusLens.Domain.Students;

public sealed class StudentSubject : Entity
{
    private StudentSubject()
    {
    }

    private StudentSubject(
        StudentSubjectType type,
        string? customName)
        : base(Guid.CreateVersion7())
    {
        Type = type;
        CustomName = customName;
    }

    public StudentSubjectType Type { get; private set; }

    public string? CustomName { get; private set; }

    public static StudentSubject Predefined(StudentSubjectType type)
    {
        if (type == StudentSubjectType.Other)
        {
            throw new ArgumentException(
                "Use Custom() for an Other subject.",
                nameof(type));
        }

        return new StudentSubject(type, null);
    }

    public static StudentSubject Custom(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Custom subject name is required.",
                nameof(name));
        }

        return new StudentSubject(
            StudentSubjectType.Other,
            name.Trim());
    }
}
