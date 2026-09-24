using System.Net.Mail;
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

public sealed class CreateStudentParentInvitationCommandHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudentParentInvitation> invitationRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IEmailSender emailSender,
    IInvitationUrlBuilder invitationUrlBuilder,
    TimeProvider timeProvider)
    : IRequestHandler<CreateStudentParentInvitationCommand, Result<InvitationResponse>>
{
    public async Task<Result<InvitationResponse>> Handle(
        CreateStudentParentInvitationCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized("Access.Unauthorized", "The current user could not be identified.");
        }

        string email = request.Request.Email?.Trim() ?? string.Empty;
        if (!IsValidEmail(email))
        {
            return Error.Validation("Access.ParentEmailInvalid", "A valid parent email is required.");
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(
            item => item.UserId == userId,
            item => item.User);

        if (student is null)
        {
            return Error.NotFound("Access.StudentNotFound", "The current user does not have a student profile.");
        }

        string normalizedEmail = email.ToUpperInvariant();
        DateTimeOffset expiresAtUtc = timeProvider.GetUtcNow().AddDays(7);
        string token = CreateToken();
        string tokenHash = HashToken(token);

        StudentParentInvitation? invitation = await invitationRepository.FirstOrDefaultAsync(item =>
            item.StudentId == student.Id &&
            item.TargetEmailNormalized == normalizedEmail &&
            item.Status == ParentInvitationStatus.Pending);

        if (invitation is null)
        {
            invitation = new StudentParentInvitation(
                student.Id,
                StudentParentInvitationType.Email,
                normalizedEmail,
                tokenHash,
                expiresAtUtc);
            invitationRepository.Add(invitation);
        }
        else
        {
            invitation.Renew(tokenHash, expiresAtUtc);
            invitationRepository.Update(invitation);
        }

        await unitOfWork.SaveChangesAsync();

        string invitationUrl = invitationUrlBuilder.CreateStudentParentInvitationUrl(token);
        string studentName = student.PreferredName
                             ?? $"{student.User.FirstName} {student.User.LastName}".Trim();

        await emailSender.SendStudentParentInvitationAsync(
            email,
            studentName,
            invitationUrl,
            cancellationToken);

        return new InvitationResponse(
            invitation.Id,
            null,
            invitation.StudentId,
            invitation.Status.ToString(),
            InvitationInitiator.Student.ToString(),
            "Outgoing",
            "StudentParent",
            email,
            invitation.ExpiresAtUtc,
            null,
            invitationUrl);
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            return new MailAddress(email).Address.Equals(email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string CreateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}