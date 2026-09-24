using System.Security.Cryptography;
using System.Text;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Contracts.Access;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Access;

public sealed class RegenerateStudentParentInvitationLinkCommandHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudentParentInvitation> invitationRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IInvitationUrlBuilder invitationUrlBuilder,
    TimeProvider timeProvider)
    : IRequestHandler<RegenerateStudentParentInvitationLinkCommand, Result<InvitationResponse>>
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    public async Task<Result<InvitationResponse>> Handle(
        RegenerateStudentParentInvitationLinkCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized(
                "Access.Unauthorized",
                "The current user could not be identified.");
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(
            item => item.UserId == userId);

        if (student is null)
        {
            return Error.NotFound(
                "Access.StudentNotFound",
                "The current user does not have a student profile.");
        }

        StudentParentInvitation? invitation = await invitationRepository.FirstOrDefaultAsync(item =>
            item.StudentId == student.Id &&
            item.Status == ParentInvitationStatus.Pending);

        if (invitation is null)
        {
            return Error.NotFound(
                "Access.InvitationNotFound",
                "No pending invitation exists for this student.");
        }

        if (invitation.Type != StudentParentInvitationType.Link)
        {
            return Error.Conflict(
                "Access.EmailLinkCannotBeRegenerated",
                "Email invitations cannot be regenerated as links.");
        }

        if (invitation.IsExpired(timeProvider.GetUtcNow()))
        {
            return Error.Conflict(
                "Access.InvitationInvalidOrExpired",
                "This invitation has expired.");
        }

        string token = CreateToken();
        string tokenHash = HashToken(token);
        DateTimeOffset expiresAtUtc = timeProvider.GetUtcNow().Add(InvitationLifetime);

        invitation.Renew(tokenHash, expiresAtUtc);
        invitationRepository.Update(invitation);
        await unitOfWork.SaveChangesAsync();

        string invitationUrl = invitationUrlBuilder.CreateStudentParentInvitationUrl(token);

        return new InvitationResponse(
            invitation.Id,
            null,
            invitation.StudentId,
            invitation.Status.ToString(),
            InvitationInitiator.Student.ToString(),
            "Outgoing",
            "StudentParent",
            null,
            invitation.ExpiresAtUtc,
            null,
            invitationUrl);
    }

    private static string CreateToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
