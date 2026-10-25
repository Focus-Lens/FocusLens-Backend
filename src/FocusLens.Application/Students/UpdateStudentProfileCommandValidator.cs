using FluentValidation;
using FocusLens.Contracts.Students;

namespace FocusLens.Application.Students;

public sealed class UpdateStudentProfileCommandValidator : AbstractValidator<UpdateStudentProfileCommand>
{
    public UpdateStudentProfileCommandValidator()
    {
        RuleFor(command => command.Request.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Request.LastName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Request.PhoneNumber).MaximumLength(32)
            .When(command => command.Request.PhoneNumber is not null);
        RuleFor(command => command.Request.Grade).IsInEnum();
        RuleFor(command => command.Request.CustomGrade)
            .NotEmpty().MaximumLength(100)
            .When(command => command.Request.Grade == StudentGrade.Other);
        RuleFor(command => command.Request.CustomGrade).Empty()
            .When(command => command.Request.Grade != StudentGrade.Other)
            .WithMessage("CustomGrade is only allowed for Other.");
    }
}
