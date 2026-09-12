using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Students;

namespace FocusLens.Domain.UnitTests.ChildSetup;

public class StudyPriorityTests
{
    [Fact]
    public void ReplaceStudyPriorities_StoresSelectedPriorities()
    {
        ChildSetupDraft draft = new(Guid.NewGuid());

        draft.ReplaceStudyPriorities(
        [
            StudyPriority.BuildStudyRoutine,
            StudyPriority.StayFocused,
            StudyPriority.ExamPreparation
        ]);

        Assert.Equal(
            [
                StudyPriority.BuildStudyRoutine,
                StudyPriority.StayFocused,
                StudyPriority.ExamPreparation
            ],
            draft.StudyPriorities);
    }

    [Fact]
    public void ReplaceStudyPriorities_ReplacesExistingPriorities()
    {
        ChildSetupDraft draft = new(Guid.NewGuid());

        draft.ReplaceStudyPriorities(
        [
            StudyPriority.BuildStudyRoutine,
            StudyPriority.StayFocused
        ]);

        draft.ReplaceStudyPriorities(
        [
            StudyPriority.ExamPreparation
        ]);

        Assert.Single(draft.StudyPriorities);
        Assert.Equal(
            StudyPriority.ExamPreparation,
            draft.StudyPriorities.First());
    }

    [Fact]
    public void ReplaceStudyPriorities_WithDuplicates_Throws()
    {
        ChildSetupDraft draft = new(Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => draft.ReplaceStudyPriorities(
        [
            StudyPriority.StayFocused,
            StudyPriority.StayFocused
        ]));
    }

    [Fact]
    public void ReplaceStudyPriorities_WithEmptyCollection_ClearsPriorities()
    {
        ChildSetupDraft draft = new(Guid.NewGuid());

        draft.ReplaceStudyPriorities(
        [
            StudyPriority.StayFocused
        ]);

        draft.ReplaceStudyPriorities([]);

        Assert.Empty(draft.StudyPriorities);
    }
}