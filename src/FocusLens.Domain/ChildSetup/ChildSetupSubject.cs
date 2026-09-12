using FocusLens.Domain.Common;
using FocusLens.Domain.Students;

namespace FocusLens.Domain.ChildSetup;

public sealed class ChildSetupSubject : Entity
{
    private ChildSetupSubject()
    {
    }

    private ChildSetupSubject(StudentSubjectType type, string? customName)
        : base(Guid.CreateVersion7())
    {
        Type = type;
        CustomName = customName;
    }

    public StudentSubjectType Type { get; private set; }
    public string? CustomName { get; private set; }

    public static ChildSetupSubject Predefined(StudentSubjectType type)
    {
        if (type == StudentSubjectType.Other)
        {
            throw new ArgumentException(
                "Use Custom() for an Other subject.",
                nameof(type));
        }

        return new ChildSetupSubject(type, null);
    }

    public static ChildSetupSubject Custom(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Custom subject name is required.",
                nameof(name));
        }

        return new ChildSetupSubject(StudentSubjectType.Other, name.Trim());
    }
}