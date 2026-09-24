using System.Security.Cryptography;
using System.Text;
using FocusLens.Contracts.Access;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Access;

public sealed class ResolveStudentParentInvitationQueryHandler(
    IBaseRepository<StudentParentInvitation> invitationRepository,
    TimeProvider timeProvider)
    : IRequestHandler<ResolveStudentParentInvitationQuery, Result<ResolveStudentParentInvitationResponse>>
{
    public async Task<Result<ResolveStudentParentInvitationResponse>> Handle(
        ResolveStudentParentInvitationQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return InvalidInvitation();
        }

        string hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(request.Token)));

        StudentParentInvitation? invitation = await invitationRepository.FirstOrDefaultAsync(
            item => item.TokenHash == hash,
            item => item.Student,
            item => item.Student.User);

        DateTimeOffset now = timeProvider.GetUtcNow();

        if (invitation is null || invitation.IsExpired(now))
        {
            return InvalidInvitation();
        }

        if (invitation.Status != ParentInvitationStatus.Pending)
        {
            return Error.Conflict(
                "Access.InvitationNoLongerPending",
                "This invitation is no longer pending.");
        }

        string displayName = invitation.Student.PreferredName
                             ?? invitation.Student.User.FirstName.Trim();

        return new ResolveStudentParentInvitationResponse(
            invitation.Id,
            invitation.Status.ToString(),
            invitation.Type.ToString(),
            invitation.TargetEmailNormalized,
            displayName,
            GetAgeRange(invitation.Student.DateOfBirth, now),
            invitation.ExpiresAtUtc,
            true);
    }

    private static string? GetAgeRange(DateOnly? dateOfBirth, DateTimeOffset now)
    {
        if (dateOfBirth is null)
        {
            return null;
        }

        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
        int age = today.Year - dateOfBirth.Value.Year;

        if (dateOfBirth.Value > today.AddYears(-age))
        {
            age--;
        }

        return age switch
        {
            >= 8 and <= 10 => "8–10",
            >= 11 and <= 12 => "11–12",
            >= 13 and <= 15 => "13–15",
            >= 16 and <= 18 => "16–18",
            _ => null
        };
    }

    private static Error InvalidInvitation()
    {
        return Error.NotFound(
            "Access.InvitationInvalidOrExpired",
            "This invitation link is invalid or has expired.");
    }
}
