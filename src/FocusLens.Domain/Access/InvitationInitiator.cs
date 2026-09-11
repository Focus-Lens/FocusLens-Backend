namespace FocusLens.Domain.Access;

/// <summary>
/// Identifies which side created a pending parent-student link request.
/// </summary>
public enum InvitationInitiator
{
    Parent = 1,
    Student = 2
}
