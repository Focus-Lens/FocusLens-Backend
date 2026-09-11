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

        if (invitation is null || invitation.IsExpired(timeProvider.GetUtcNow()))
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
            ?? $"{invitation.Student.User.FirstName} {invitation.Student.User.LastName}".Trim();

        return new ResolveStudentParentInvitationResponse(
            invitation.Status.ToString(),
            displayName,
            invitation.ExpiresAtUtc,
            RequiresSignIn: true);
    }

    private static Error InvalidInvitation()
        => Error.NotFound(
            "Access.InvitationInvalidOrExpired",
            "This invitation link is invalid or has expired.");
}
