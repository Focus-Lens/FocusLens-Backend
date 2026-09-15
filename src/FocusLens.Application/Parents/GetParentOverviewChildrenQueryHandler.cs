using FocusLens.Contracts;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Interfaces;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Parents;

public sealed class GetParentOverviewChildrenQueryHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    IBaseRepository<Student> studentRepository,
    IBaseRepository<ChildSetupDraft> childSetupDraftRepository,
    IBaseRepository<ChildSetupInvitation> childSetupInvitationRepository,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<GetParentOverviewChildrenQuery, IReadOnlyList<ParentOverviewChildResponse>>
{
    private const string ActiveChildType = "ActiveChild";
    private const string ChildSetupType = "ChildSetup";

    public async Task<IReadOnlyList<ParentOverviewChildResponse>> Handle(
        GetParentOverviewChildrenQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return [];
        }

        Parent? parent = await parentRepository.FirstOrDefaultAsync(parent => parent.UserId == userId);

        if (parent is null)
        {
            return [];
        }

        List<ParentOverviewChildResponse> children = [];

        IEnumerable<ParentStudentRelationship> activeRelationships =
            await relationshipRepository.GetAllAsync(relationship =>
                relationship.ParentId == parent.Id &&
                relationship.Status == RelationshipStatus.Active);

        ParentStudentRelationship[] activeRelationshipList = activeRelationships.ToArray();
        Guid[] activeStudentIds = activeRelationshipList
            .Select(relationship => relationship.StudentId)
            .Distinct()
            .ToArray();

        if (activeStudentIds.Length > 0)
        {
            IEnumerable<Student> activeStudents = await studentRepository.GetAllAsync(
                student => activeStudentIds.Contains(student.Id),
                student => student.User);

            IReadOnlyDictionary<Guid, Student> studentsById =
                activeStudents.ToDictionary(student => student.Id);

            foreach (ParentStudentRelationship relationship in activeRelationshipList)
            {
                if (!studentsById.TryGetValue(relationship.StudentId, out Student? student))
                {
                    continue;
                }

                children.Add(new ParentOverviewChildResponse(
                    relationship.Id,
                    ActiveChildType,
                    relationship.Status.ToString(),
                    student.User.FirstName,
                    student.User.LastName,
                    student.Id,
                    null,
                    null,
                    null,
                    null));
            }
        }

        IEnumerable<ChildSetupDraft> setupDrafts = await childSetupDraftRepository.GetAllAsync(
            draft => draft.ParentId == parent.Id &&
                     draft.Status != ChildSetupStatus.Activated);

        ChildSetupDraft[] setupDraftList = setupDrafts
            .Where(draft => !draft.ClaimedByStudentId.HasValue ||
                            !activeStudentIds.Contains(draft.ClaimedByStudentId.Value))
            .ToArray();

        if (setupDraftList.Length == 0)
        {
            return children;
        }

        Guid[] setupDraftIds = setupDraftList
            .Select(draft => draft.Id)
            .ToArray();

        DateTimeOffset utcNow = timeProvider.GetUtcNow();
        IEnumerable<ChildSetupInvitation> pendingInvitations =
            await childSetupInvitationRepository.GetAllAsync(invitation =>
                setupDraftIds.Contains(invitation.ChildSetupDraftId) &&
                invitation.Status == ChildSetupInvitationStatus.Pending &&
                invitation.ExpiresAtUtc > utcNow);

        IReadOnlyDictionary<Guid, ChildSetupInvitation> pendingInvitationsByDraftId =
            pendingInvitations
                .GroupBy(invitation => invitation.ChildSetupDraftId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(invitation => invitation.ExpiresAtUtc)
                        .First());

        foreach (ChildSetupDraft draft in setupDraftList)
        {
            pendingInvitationsByDraftId.TryGetValue(
                draft.Id,
                out ChildSetupInvitation? invitation);

            children.Add(new ParentOverviewChildResponse(
                draft.Id,
                ChildSetupType,
                draft.Status.ToString(),
                draft.FirstName,
                draft.LastName,
                draft.ClaimedByStudentId,
                draft.Id,
                invitation?.Id,
                invitation?.Status.ToString(),
                invitation?.ExpiresAtUtc));
        }

        return children;
    }
}
