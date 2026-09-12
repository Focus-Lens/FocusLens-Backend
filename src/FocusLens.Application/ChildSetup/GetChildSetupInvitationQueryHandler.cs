using System.Security.Cryptography;
using System.Text;
using FocusLens.Application.Common.Mappings;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Contracts.Students;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.ChildSetup;

public sealed class GetChildSetupInvitationQueryHandler(
    IBaseRepository<ChildSetupInvitation> invitationRepository,
    IBaseRepository<ChildSetupDraft> draftRepository,
    TimeProvider timeProvider)
    : IRequestHandler<
        GetChildSetupInvitationQuery,
        Result<ChildSetupInvitationDetailsResponse>>
{
    public async Task<Result<ChildSetupInvitationDetailsResponse>> Handle(
        GetChildSetupInvitationQuery request,
        CancellationToken cancellationToken)
    {
        string token = request.Token?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(token))
        {
            return Error.NotFound(
                "ChildSetupInvitation.NotFound",
                "The invitation could not be found.");
        }

        string tokenHash = HashToken(token);

        ChildSetupInvitation? invitation =
            await invitationRepository.FirstOrDefaultAsync(item => item.TokenHash == tokenHash);

        if (invitation is null)
        {
            return Error.NotFound(
                "ChildSetupInvitation.NotFound",
                "The invitation could not be found.");
        }

        if (invitation.Status != ChildSetupInvitationStatus.Pending)
        {
            return Error.Conflict(
                "ChildSetupInvitation.Unavailable",
                "This invitation is no longer available.");
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        if (invitation.IsExpired(now))
        {
            return Error.Conflict(
                "ChildSetupInvitation.Expired",
                "This invitation has expired.");
        }

        ChildSetupDraft? draft = await draftRepository.GetByIdAsync(
            invitation.ChildSetupDraftId,
            item => item.Subjects);

        if (draft is null)
        {
            return Error.NotFound(
                "ChildSetupInvitation.NotFound",
                "The invitation could not be found.");
        }

        return new ChildSetupInvitationDetailsResponse(
            invitation.Status.ToString(),
            draft.FirstName,
            draft.LastName,
            draft.Grade is null
                ? null
                : StudentEnumMapper.ToContract(draft.Grade.Value),
            draft.Subjects
                .Select(subject => new ChildSetupInvitationSubjectResponse(
                    subject.Type.ToString(),
                    subject.CustomName))
                .ToList(),
            draft.StudyPriorities
                .Select(priority => Enum.Parse<StudyPriority>(priority.ToString()))
                .ToList(),
            draft.StudyTimeGoal is null
                ? null
                : new StudyTimeGoalResponse(
                    draft.StudyTimeGoal.Period.ToString(),
                    draft.StudyTimeGoal.TargetMinutes,
                  draft.StudyTimeGoal.Days,
                  draft.StudyTimeGoal.StartDate),
            invitation.ExpiresAtUtc);
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}