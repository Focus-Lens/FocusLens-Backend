using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Students;

namespace FocusLens.Domain.UnitTests.ChildSetup;

public class ChildSetupDraftTests
{
    [Fact]
    public void Constructor_WithValidParentId_CreatesDraft()
    {
        Guid parentId = Guid.NewGuid();

        ChildSetupDraft draft = new(parentId);

        Assert.Equal(parentId, draft.ParentId);
        Assert.Equal(ChildSetupStatus.Draft, draft.Status);
        Assert.Empty(draft.Subjects);
        Assert.Null(draft.StudyTimeGoal);
    }

    [Fact]
    public void Constructor_WithEmptyParentId_Throws() =>
        Assert.Throws<ArgumentException>(() => new ChildSetupDraft(Guid.Empty));

    [Fact]
    public void SetName_TrimsAndStoresNames()
    {
        ChildSetupDraft draft = new(Guid.NewGuid());

        draft.SetName(" Karim ", " Mahmoud ");

        Assert.Equal("Karim", draft.FirstName);
        Assert.Equal("Mahmoud", draft.LastName);
    }

    [Fact]
    public void SetName_WithMissingFirstName_Throws()
    {
        ChildSetupDraft draft = new(Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => draft.SetName(" ", "Mahmoud"));
    }

    [Fact]
    public void SetName_WithMissingLastName_Throws()
    {
        ChildSetupDraft draft = new(Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => draft.SetName("Karim", " "));
    }

    [Fact]
    public void ReplaceSubjects_ReplacesExistingSubjects()
    {
        ChildSetupDraft draft = new(Guid.NewGuid());

        ChildSetupSubject math =
            ChildSetupSubject.Predefined(StudentSubjectType.Math);

        ChildSetupSubject economics =
            ChildSetupSubject.Custom("Economics");

        draft.ReplaceSubjects([math, economics]);

        Assert.Equal(2, draft.Subjects.Count);
        Assert.Contains(math, draft.Subjects);
        Assert.Contains(economics, draft.Subjects);
    }

    [Fact]
    public void SetStudyTimeGoal_StoresGoal()
    {
        ChildSetupDraft draft = new(Guid.NewGuid());

        StudyTimeGoal goal =
            StudyTimeGoal.Create(
                StudyTimeGoalPeriod.Daily,
                60,
                [DayOfWeek.Monday],
                new DateOnly(2026, 9, 14)).Value;

        draft.SetStudyTimeGoal(goal);

        Assert.Equal(goal, draft.StudyTimeGoal);
    }

    [Fact]
    public void MarkInvited_FromDraft_MovesToInvited()
    {
        ChildSetupDraft draft = new(Guid.NewGuid());

        draft.MarkInvited();

        Assert.Equal(ChildSetupStatus.Invited, draft.Status);
    }

    [Fact]
    public void MarkClaimed_FromInvited_MovesToClaimed()
    {
        ChildSetupDraft draft = new(Guid.NewGuid());

        Guid studentId = Guid.NewGuid();

        draft.MarkInvited();
        draft.MarkClaimed(studentId);

        Assert.Equal(ChildSetupStatus.Claimed, draft.Status);
        Assert.Equal(studentId, draft.ClaimedByStudentId);
    }

    [Fact]
    public void MarkActivated_FromClaimed_MovesToActivated()
    {
        ChildSetupDraft draft = new(Guid.NewGuid());

        Guid studentId = Guid.NewGuid();

        draft.MarkInvited();
        draft.MarkClaimed(studentId);
        draft.MarkActivated();

        Assert.Equal(ChildSetupStatus.Activated, draft.Status);
        Assert.Equal(studentId, draft.ClaimedByStudentId);
    }

    [Fact]
    public void MarkClaimed_FromDraft_Throws()
    {
        ChildSetupDraft draft = new(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => draft.MarkClaimed(Guid.NewGuid()));
    }

    [Fact]
    public void MarkActivated_FromDraft_Throws()
    {
        ChildSetupDraft draft = new(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(
            draft.MarkActivated);
    }
}
