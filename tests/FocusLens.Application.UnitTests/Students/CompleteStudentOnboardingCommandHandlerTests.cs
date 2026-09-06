using FluentValidation.TestHelper;
using FocusLens.Application.Students;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;
using ContractStudentSubjectType = FocusLens.Contracts.Students.StudentSubjectType;
using ContractStudentGoal = FocusLens.Contracts.Students.StudentGoal;
using ContractStudentGrade = FocusLens.Contracts.Students.StudentGrade;
using DomainStudentSubjectType = FocusLens.Domain.Students.StudentSubjectType;

namespace FocusLens.Application.UnitTests.Students;

public class CompleteStudentOnboardingCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenAllPreferencesAreSkipped_LeavesOnboardingIncomplete()
    {
        Student student = new(Guid.NewGuid());
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CompleteStudentOnboardingCommandHandler(
            new InMemoryRepository<Student>(student),
            new FakeCurrentUser(student.UserId),
            unitOfWork);

        Result<Success> result = await handler.Handle(
            new CompleteStudentOnboardingCommand(
                new CompleteStudentOnboardingRequest(null, null, null)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(student.IsOnboardingCompleted);
        Assert.Null(student.Goal);
        Assert.Null(student.Grade);
        Assert.Empty(student.Subjects);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WhenOnlySubjectsAreProvided_LeavesOnboardingIncomplete()
    {
        Student student = new(Guid.NewGuid());
        var handler = new CompleteStudentOnboardingCommandHandler(
            new InMemoryRepository<Student>(student),
            new FakeCurrentUser(student.UserId),
            new FakeUnitOfWork());

        Result<Success> result = await handler.Handle(
            new CompleteStudentOnboardingCommand(
                new CompleteStudentOnboardingRequest(
                    null,
                    null,
                    [new StudentSubjectRequest(ContractStudentSubjectType.Math, null)])),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(student.IsOnboardingCompleted);
        Assert.Single(student.Subjects);
        Assert.Equal(DomainStudentSubjectType.Math, student.Subjects.Single().Type);
    }

    [Fact]
    public async Task Handle_WhenAllPreferencesAreProvided_CompletesOnboarding()
    {
        Student student = new(Guid.NewGuid());
        var handler = new CompleteStudentOnboardingCommandHandler(
            new InMemoryRepository<Student>(student),
            new FakeCurrentUser(student.UserId),
            new FakeUnitOfWork());

        Result<Success> result = await handler.Handle(
            new CompleteStudentOnboardingCommand(
                new CompleteStudentOnboardingRequest(
                    ContractStudentGoal.FocusBetter,
                    ContractStudentGrade.Grade10,
                    [new StudentSubjectRequest(ContractStudentSubjectType.Math, null)])),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(student.IsOnboardingCompleted);
    }

    [Fact]
    public void Validator_WhenPreferencesAreSkipped_HasNoErrors()
    {
        var validator = new CompleteStudentOnboardingCommandValidator();

        var result = validator.TestValidate(
            new CompleteStudentOnboardingCommand(
                new CompleteStudentOnboardingRequest(null, null, null)));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validator_WhenProvidedSubjectsAreInvalid_ReturnsError()
    {
        var validator = new CompleteStudentOnboardingCommandValidator();

        var result = validator.TestValidate(
            new CompleteStudentOnboardingCommand(
                new CompleteStudentOnboardingRequest(
                    null,
                    null,
                    [new StudentSubjectRequest(ContractStudentSubjectType.Other, null)])));

        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith("CustomName"));
    }
}
