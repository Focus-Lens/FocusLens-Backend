using FocusLens.Application.Students;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;
using FocusLens.Domain.Identity;
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
        Assert.Equal(StudentGoal.FocusBetter, student.Goal);
        Assert.Equal(StudentGrade.Grade11, student.Grade);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WhenGoalIsNull_ClearsExistingGoal()
    {
        Student student = CreateStudentWithPreferences();

        Result<StudentDetailsResponse> result = await CreateHandler(student).Handle(
            new UpdateStudentPreferencesCommand(new UpdateStudentPreferencesRequest { Goal = null }),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(student.Goal);
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
        Assert.Equal(StudentGoal.FocusBetter, student.Goal);
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
    public async Task Handle_WhenDateOfBirthIsProvided_UpdatesAndReturnsDateOfBirth()
    {
        Student student = CreateStudentWithPreferences();
        student.SetDateOfBirth(new DateOnly(2010, 5, 12));

        Result<StudentDetailsResponse> result = await CreateHandler(student).Handle(
            new UpdateStudentPreferencesCommand(new UpdateStudentPreferencesRequest
            {
                DateOfBirth = new DateOnly(2011, 6, 15)
            }),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2011, 6, 15), student.DateOfBirth);
        Assert.Equal(new DateOnly(2011, 6, 15), result.Value.DateOfBirth);
    }

    [Fact]
    public async Task Handle_WhenDateOfBirthIsNull_ClearsExistingDateOfBirth()
    {
        Student student = CreateStudentWithPreferences();
        student.SetDateOfBirth(new DateOnly(2010, 5, 12));

        Result<StudentDetailsResponse> result = await CreateHandler(student).Handle(
            new UpdateStudentPreferencesCommand(new UpdateStudentPreferencesRequest
            {
                DateOfBirth = null
            }),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(student.DateOfBirth);
        Assert.Null(result.Value.DateOfBirth);
    }

    [Fact]
    public async Task Handle_WhenCustomGradeIsProvidedForOther_StoresTrimmedValue()
    {
        Student student = CreateStudentWithPreferences();
        student.SetGrade(StudentGrade.Other);

        Result<StudentDetailsResponse> result = await CreateHandler(student).Handle(
            new UpdateStudentPreferencesCommand(new UpdateStudentPreferencesRequest
            {
                CustomGrade = "  Year 13  "
            }),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Year 13", student.CustomGrade);
        Assert.Equal("Year 13", result.Value.CustomGrade);
        Assert.True(result.Value.OnboardingCompleted);
    }

    [Fact]
    public async Task Handle_WhenCustomGradeIsProvidedForPredefinedGrade_ReturnsValidationError()
    {
        Student student = CreateStudentWithPreferences();

        Result<StudentDetailsResponse> result = await CreateHandler(student).Handle(
            new UpdateStudentPreferencesCommand(new UpdateStudentPreferencesRequest
            {
                CustomGrade = "Year 13"
            }),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Students.InvalidCustomGrade", result.TopError.Code);
        Assert.Null(student.CustomGrade);
    }

    [Fact]
    public async Task Handle_WhenGradeChangesFromOtherToPredefined_ClearsCustomGrade()
    {
        Student student = CreateStudentWithPreferences();
        student.SetGrade(StudentGrade.Other);
        student.SetCustomGrade("Year 13");

        Result<StudentDetailsResponse> result = await CreateHandler(student).Handle(
            new UpdateStudentPreferencesCommand(new UpdateStudentPreferencesRequest
            {
                Grade = ContractStudentGrade.Grade12
            }),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(StudentGrade.Grade12, student.Grade);
        Assert.Null(student.CustomGrade);
        Assert.Null(result.Value.CustomGrade);
    }

    [Fact]
    public async Task Handle_WhenStudyTimeGoalChangesAndStudentHasActiveParent_ReturnsForbidden()
    {
        Student student = CreateStudentWithPreferences();
        ParentStudentRelationship relationship = new(Guid.NewGuid(), student.Id);
        relationship.Accept();

        UpdateStudentPreferencesRequest request = new()
        {
            StudyTimeGoal = new(
                FocusLens.Contracts.Students.StudyTimeGoalPeriod.Daily,
                60,
                null)
        };

        Result<StudentDetailsResponse> result = await CreateHandler(student, relationships: [relationship]).Handle(
            new UpdateStudentPreferencesCommand(request),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Students.StudyTimeGoalControlledByParent", result.TopError.Code);
        Assert.Null(student.StudyTimeGoal);
    }

    [Fact]
    public async Task Handle_WhenStudyTimeGoalChangesAndStudentHasNoParent_UpdatesGoal()
    {
        Student student = CreateStudentWithPreferences();

        Result<StudentDetailsResponse> result = await CreateHandler(student).Handle(
            new UpdateStudentPreferencesCommand(new UpdateStudentPreferencesRequest
            {
                StudyTimeGoal = new(
                    FocusLens.Contracts.Students.StudyTimeGoalPeriod.Daily,
                    60,
                    [DayOfWeek.Monday],
                    new DateOnly(2026, 9, 14))
            }),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(student.StudyTimeGoal);
        Assert.Equal(60, student.StudyTimeGoal.TargetMinutes);
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
        FakeUnitOfWork? unitOfWork = null,
        IEnumerable<ParentStudentRelationship>? relationships = null)
    {
        return new UpdateStudentPreferencesCommandHandler(
            new InMemoryRepository<Student>(student),
            relationships is null || !relationships.Any()
                ? new InMemoryRepository<ParentStudentRelationship>()
                : new InMemoryRepository<ParentStudentRelationship>(relationships.First()),
            new FakeNotificationWriter(),
            new FakeCurrentUser(student.UserId),
            unitOfWork ?? new FakeUnitOfWork());
    }

    private static Student CreateStudentWithPreferences()
    {
        Student student = new(Guid.NewGuid());
        student.SetPrivateProperty("User", new ApplicationUser
        {
            FirstName = "Test",
            LastName = "Student",
            Email = "test@example.com"
        });
        student.CompleteOnboarding(
            StudentGoal.FocusBetter,
            StudentGrade.Grade10,
            [StudentSubject.Predefined(StudentSubjectType.Math)]);
        return student;
    }
}
