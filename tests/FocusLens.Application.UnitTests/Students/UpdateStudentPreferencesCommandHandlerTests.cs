using FocusLens.Application.Students;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Domain;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;
using StudentDetailsResponse = FocusLens.Contracts.Students.StudentDetailsResponse;
using ContractStudentGrade = FocusLens.Contracts.Students.StudentGrade;
using UpdateStudentPreferencesRequest = FocusLens.Contracts.Students.UpdateStudentPreferencesRequest;

namespace FocusLens.Application.UnitTests.Students;

public class UpdateStudentPreferencesCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenGoalIsOmitted_PreservesExistingGoal()
    {
        Student student = CreateStudentWithPreferences();
        FakeUnitOfWork unitOfWork = new();

        Result<StudentDetailsResponse> result = await CreateHandler(student, unitOfWork).Handle(
            new UpdateStudentPreferencesCommand(new UpdateStudentPreferencesRequest
            {
                Grade = ContractStudentGrade.Grade11
            }),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(student.Goals);
        Assert.Equal(StudentGoal.FocusBetter, student.Goals.Single());
        Assert.Equal(StudentGrade.Grade11, student.Grade);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WhenGoalIsNull_ClearsExistingGoal()
    {
        Student student = CreateStudentWithPreferences();

        Result<StudentDetailsResponse> result = await CreateHandler(student).Handle(
            new UpdateStudentPreferencesCommand(new UpdateStudentPreferencesRequest { Goals = null }),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(student.Goals);
        Assert.Equal(StudentGrade.Grade10, student.Grade);
        Assert.Single(student.Subjects);
    }

    [Fact]
    public async Task Handle_WhenSubjectsAreEmpty_ClearsExistingSubjects()
    {
        Student student = CreateStudentWithPreferences();

        Result<StudentDetailsResponse> result = await CreateHandler(student).Handle(
            new UpdateStudentPreferencesCommand(new UpdateStudentPreferencesRequest { Subjects = [] }),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(student.Subjects);
        Assert.Single(student.Goals);
        Assert.Equal(StudentGoal.FocusBetter, student.Goals.Single());
        Assert.Equal(StudentGrade.Grade10, student.Grade);
    }

    [Fact]
    public async Task Handle_WhenSubjectsAreOmitted_PreservesExistingSubjects()
    {
        Student student = CreateStudentWithPreferences();

        Result<StudentDetailsResponse> result = await CreateHandler(student).Handle(
            new UpdateStudentPreferencesCommand(new UpdateStudentPreferencesRequest
            {
                Grade = ContractStudentGrade.Grade11
            }),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(student.Subjects);
        Assert.Equal(StudentSubjectType.Math, student.Subjects.Single().Type);
    }

    [Fact]
    public async Task Handle_WhenSubjectsAreNull_ReturnsValidationError()
    {
        Student student = CreateStudentWithPreferences();

        Result<StudentDetailsResponse> result = await CreateHandler(student).Handle(
            new UpdateStudentPreferencesCommand(new UpdateStudentPreferencesRequest { Subjects = null }),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Students.InvalidSubjects", result.TopError.Code);
        Assert.Single(student.Subjects);
    }

    [Fact]
    public async Task Handle_WhenPreferredNameIsProvided_TrimsAndStoresIt()
    {
        Student student = CreateStudentWithPreferences();

        Result<StudentDetailsResponse> result = await CreateHandler(student).Handle(
            new UpdateStudentPreferencesCommand(new UpdateStudentPreferencesRequest { PreferredName = "  كريم  " }),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("كريم", student.PreferredName);
        Assert.Equal("كريم", result.Value.PreferredName);
    }

    [Fact]
    public async Task Handle_WhenPreferredNameIsNull_ClearsIt()
    {
        Student student = CreateStudentWithPreferences();
        student.SetPreferredName("Karim");

        Result<StudentDetailsResponse> result = await CreateHandler(student).Handle(
            new UpdateStudentPreferencesCommand(new UpdateStudentPreferencesRequest { PreferredName = null }),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(student.PreferredName);
        Assert.Null(result.Value.PreferredName);
    }

    [Fact]
    public async Task Handle_WhenPreferredNameIsWhitespace_ReturnsValidationError()
    {
        Student student = CreateStudentWithPreferences();

        Result<StudentDetailsResponse> result = await CreateHandler(student).Handle(
            new UpdateStudentPreferencesCommand(new UpdateStudentPreferencesRequest { PreferredName = "   " }),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Students.PreferredNameRequired", result.TopError.Code);
        Assert.Null(student.PreferredName);
    }

    private static UpdateStudentPreferencesCommandHandler CreateHandler(
        Student student,
        FakeUnitOfWork? unitOfWork = null)
    {
        return new UpdateStudentPreferencesCommandHandler(
            new InMemoryRepository<Student>(student),
            new FakeCurrentUser(student.UserId),
            unitOfWork ?? new FakeUnitOfWork());
    }

    private static Student CreateStudentWithPreferences()
    {
        Student student = new(Guid.NewGuid());
        student.CompleteOnboarding(
            new[] { StudentGoal.FocusBetter },
            StudentGrade.Grade10,
            [StudentSubject.Predefined(StudentSubjectType.Math)]);
        return student;
    }
}