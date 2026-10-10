using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Students;

namespace FocusLens.Domain.UnitTests.ChildSetup;

public class StudentGoalTests
{
    [Fact]
    public void SetGoal_StoresSelectedGoal()
    {
        ChildSetupDraft draft = new(Guid.NewGuid());

        draft.SetGoal(StudentGoal.FocusBetter);

        Assert.Equal(
            StudentGoal.FocusBetter,
            draft.Goal);
    }

    [Fact]
    public void SetGoal_ReplacesExistingGoal()
    {
        ChildSetupDraft draft = new(Guid.NewGuid());

        draft.SetGoal(StudentGoal.FocusBetter);
        draft.SetGoal(StudentGoal.PrepareForExams);

        Assert.Equal(
            StudentGoal.PrepareForExams,
            draft.Goal);
    }

    [Fact]
    public void SetGoal_WithNull_ClearsGoal()
    {
        ChildSetupDraft draft = new(Guid.NewGuid());

        draft.SetGoal(StudentGoal.FocusBetter);

        draft.SetGoal(null);

        Assert.Null(draft.Goal);
    }
}