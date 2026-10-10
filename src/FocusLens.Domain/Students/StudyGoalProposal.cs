using FocusLens.Domain.Common;

namespace FocusLens.Domain.Students;

public sealed class StudyGoalProposal : AuditableEntity
{
    private StudyGoalProposal()
    {
    }

    public StudyGoalProposal(
        Guid parentId,
        Guid studentId,
        StudyTimeGoal goal)
        : base(Guid.CreateVersion7())
    {
        if (parentId == Guid.Empty)
        {
            throw new ArgumentException("Parent ID cannot be empty.", nameof(parentId));
        }

        if (studentId == Guid.Empty)
        {
            throw new ArgumentException("Student ID cannot be empty.", nameof(studentId));
        }

        ParentId = parentId;
        StudentId = studentId;
        Goal = goal ?? throw new ArgumentNullException(nameof(goal));
        Status = StudyGoalProposalStatus.Pending;
    }

    public Guid ParentId { get; private set; }

    public Guid StudentId { get; private set; }

    public StudyTimeGoal Goal { get; private set; } = null!;

    public StudyGoalProposalStatus Status { get; private set; }

    public DateTimeOffset? RespondedAtUtc { get; private set; }

    public Parent Parent { get; private set; } = null!;

    public Student Student { get; private set; } = null!;

    public void Accept(DateTimeOffset now)
    {
        EnsurePending();
        Status = StudyGoalProposalStatus.Accepted;
        RespondedAtUtc = now;
    }

    public void Reject(DateTimeOffset now)
    {
        EnsurePending();
        Status = StudyGoalProposalStatus.Rejected;
        RespondedAtUtc = now;
    }

    public void Cancel(DateTimeOffset now)
    {
        EnsurePending();
        Status = StudyGoalProposalStatus.Cancelled;
        RespondedAtUtc = now;
    }

    private void EnsurePending()
    {
        if (Status != StudyGoalProposalStatus.Pending)
        {
            throw new InvalidOperationException(
                "Only a pending study goal proposal can be changed.");
        }
    }
}