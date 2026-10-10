using FocusLens.Domain.Common;
using FocusLens.Domain.Identity;

namespace FocusLens.Domain;

public class Parent : AuditableEntity
{
    public Parent(Guid userId)
        : base(Guid.CreateVersion7())
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        }

        UserId = userId;
    }

    public Guid UserId { get; private set; }

    public DayOfWeek? WeekStartsOn { get; private set; }

    public ApplicationUser User { get; private set; } = null!;

    public void SetWeekStartsOn(DayOfWeek weekStartsOn)
    {
        if (!Enum.IsDefined(weekStartsOn))
        {
            throw new ArgumentOutOfRangeException(nameof(weekStartsOn), weekStartsOn, "Week start day is invalid.");
        }

        WeekStartsOn = weekStartsOn;
    }
}