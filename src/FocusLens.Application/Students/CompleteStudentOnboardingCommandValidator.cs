using FluentValidation;
using FocusLens.Contracts.Students;
using ContractStudentSubjectType = FocusLens.Contracts.Students.StudentSubjectType;

namespace FocusLens.Application.Students;

public sealed class CompleteStudentOnboardingCommandValidator
    : AbstractValidator<CompleteStudentOnboardingCommand>
{
    public CompleteStudentOnboardingCommandValidator()
    {
        RuleFor(command => command.Request.PreferredName)
            .Cascade(CascadeMode.Stop)
            .Must(preferredName => !string.IsNullOrWhiteSpace(preferredName))
            .WithErrorCode("Students.PreferredNameRequired")
            .WithMessage("Preferred name cannot be empty.")
            .Must(preferredName => preferredName!.Trim().Length <= 100)
            .WithErrorCode("Students.PreferredNameTooLong")
            .WithMessage("Preferred name cannot exceed 100 characters.");

        RuleFor(command => command.Request.Goal)
            .IsInEnum()
            .When(command => command.Request.Goal.HasValue);

        RuleFor(command => command.Request.Grade)
            .IsInEnum()
            .When(command => command.Request.Grade.HasValue);

        RuleFor(command => command.Request.CustomGrade)
            .Must(customGrade => !string.IsNullOrWhiteSpace(customGrade))
            .MaximumLength(100)
            .When(command => command.Request.Grade == StudentGrade.Other)
            .WithMessage("A custom grade is required and cannot exceed 100 characters.");

        RuleFor(command => command.Request.CustomGrade)
            .Empty()
            .WithMessage("CustomGrade is only allowed for Other.")
            .When(command => command.Request.Grade.HasValue && command.Request.Grade != StudentGrade.Other);

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