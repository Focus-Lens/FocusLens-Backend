using FluentValidation;

namespace FocusLens.Application.Students;

public sealed class ReplaceStudentSubjectsCommandValidator : AbstractValidator<ReplaceStudentSubjectsCommand>
{
    public ReplaceStudentSubjectsCommandValidator()
    {
        RuleFor(command => command.Request.SubjectIds).NotNull();
        RuleForEach(command => command.Request.SubjectIds).NotEmpty();
        RuleFor(command => command.Request.SubjectIds)
            .Must(subjectIds => subjectIds is null || subjectIds.Distinct().Count() == subjectIds.Count)
            .WithErrorCode("Students.DuplicateSubjects")
            .WithMessage("Subject IDs must be unique.");
    }
}
