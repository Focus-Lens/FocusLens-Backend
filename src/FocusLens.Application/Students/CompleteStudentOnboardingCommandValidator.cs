using ContractStudentSubjectType = FocusLens.Contracts.Students.StudentSubjectType;
using FluentValidation;
using FocusLens.Contracts.Students;
using FocusLens.Domain.Students;

namespace FocusLens.Application.Students;

public sealed class CompleteStudentOnboardingCommandValidator
    : AbstractValidator<CompleteStudentOnboardingCommand>
{
    public CompleteStudentOnboardingCommandValidator()
    {
        RuleFor(command => command.Request.Goal)
            .IsInEnum()
            .When(command => command.Request.Goal.HasValue);

        RuleFor(command => command.Request.Grade)
            .IsInEnum()
            .When(command => command.Request.Grade.HasValue);

        RuleFor(command => command.Request.Subjects)
            .Must(HaveUniqueSubjects)
            .WithMessage("Subjects must be unique.")
            .When(command => command.Request.Subjects is not null);

        RuleForEach(command => command.Request.Subjects!)
            .ChildRules(subject =>
            {
                subject.RuleFor(item => item.Type)
                    .IsInEnum();

                subject.When(
                    item => item.Type == ContractStudentSubjectType.Other,
                    () =>
                    {
                        subject.RuleFor(item => item.CustomName)
                            .NotEmpty()
                            .MaximumLength(200);
                    });

                subject.When(
                    item => item.Type != ContractStudentSubjectType.Other,
                    () =>
                    {
                        subject.RuleFor(item => item.CustomName)
                            .Empty()
                            .WithMessage("CustomName must be empty for predefined subjects.");
                    });
            });
    }

    private static bool HaveUniqueSubjects(
        IReadOnlyCollection<StudentSubjectRequest>? subjects)
    {
        if (subjects is null)
        {
            return true;
        }

        return subjects
            .Select(subject =>
                subject.Type == ContractStudentSubjectType.Other
                    ? $"Other:{subject.CustomName?.Trim().ToUpperInvariant()}"
                    : subject.Type.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == subjects.Count;
    }
}
