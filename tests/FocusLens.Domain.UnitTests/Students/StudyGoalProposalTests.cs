using FocusLens.Domain.Students;

namespace FocusLens.Domain.UnitTests.Students;

public sealed class StudyGoalProposalTests
{
    [Fact]
    public void Accept_WhenPending_MarksAcceptedAndStoresResponseTime()
    {
        StudyGoalProposal proposal = CreateProposal();
        DateTimeOffset now = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);

        proposal.Accept(now);

        Assert.Equal(StudyGoalProposalStatus.Accepted, proposal.Status);
        Assert.Equal(now, proposal.RespondedAtUtc);
    }

    [Fact]
    public void Reject_WhenPending_MarksRejectedAndStoresResponseTime()
    {
        StudyGoalProposal proposal = CreateProposal();
        DateTimeOffset now = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);

        proposal.Reject(now);

        Assert.Equal(StudyGoalProposalStatus.Rejected, proposal.Status);
        Assert.Equal(now, proposal.RespondedAtUtc);
    }

    [Fact]
    public void Cancel_WhenPending_MarksCancelledAndStoresResponseTime()
    {
        StudyGoalProposal proposal = CreateProposal();
        DateTimeOffset now = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);

        proposal.Cancel(now);

        Assert.Equal(StudyGoalProposalStatus.Cancelled, proposal.Status);
        Assert.Equal(now, proposal.RespondedAtUtc);
    }

    [Fact]
    public void Reject_WhenAlreadyAccepted_Throws()
    {
        StudyGoalProposal proposal = CreateProposal();
        proposal.Accept(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => proposal.Reject(DateTimeOffset.UtcNow));
    }

    private static StudyGoalProposal CreateProposal()
    {
        var goal = StudyTimeGoal.Create(
            StudyTimeGoalPeriod.Weekly,
            240,
            [DayOfWeek.Monday],
            new DateOnly(2026, 9, 14));

        return new StudyGoalProposal(Guid.NewGuid(), Guid.NewGuid(), goal.Value);
    }
}
