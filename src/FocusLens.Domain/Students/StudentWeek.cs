using FocusLens.Domain.Common;

namespace FocusLens.Domain.Students;

public sealed class StudentWeek : AuditableEntity
{
    private StudentWeek() { }

    public StudentWeek(Guid studentId, DateOnly startsOn) : base(Guid.CreateVersion7())
    {
        if (studentId == Guid.Empty)
        {
            throw new ArgumentException("Student ID is required.", nameof(studentId));
        }

        StudentId = studentId;
        StartsOn = startsOn;
        EndsOn = startsOn.AddDays(6);
    }

    public Guid StudentId { get; private set; }
    public DateOnly StartsOn { get; private set; }
    public DateOnly EndsOn { get; private set; }
    public Student Student { get; private set; } = null!;
}