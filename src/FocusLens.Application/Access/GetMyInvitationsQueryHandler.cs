using FocusLens.Contracts.Access;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Access;

public sealed class GetMyInvitationsQueryHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<Student> studentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    IBaseRepository<StudentParentInvitation> invitationRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyInvitationsQuery, IReadOnlyList<InvitationResponse>>
{
    public async Task<IReadOnlyList<InvitationResponse>> Handle(
        GetMyInvitationsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return [];
        }

        Parent? parent = await parentRepository.FirstOrDefaultAsync(parent => parent.UserId == userId);

        Student? student = await studentRepository.FirstOrDefaultAsync(student => student.UserId == userId);

        if (parent is null && student is null)
        {
            return [];
        }

        Guid? parentId = parent?.Id;
        Guid? studentId = student?.Id;

        IEnumerable<ParentStudentRelationship> relationships =
            await relationshipRepository.GetAllAsync(relationship =>
                (parentId.HasValue && relationship.ParentId == parentId.Value) ||
                (studentId.HasValue && relationship.StudentId == studentId.Value));

        List<ParentStudentRelationship> relationshipList = relationships.ToList();
        Guid[] parentIds = relationshipList
            .Select(relationship => relationship.ParentId)
            .Distinct()
            .ToArray();

        IEnumerable<Parent> invitationParents = await parentRepository.GetAllAsync(
            parent => parentIds.Contains(parent.Id),
            parent => parent.User);

        IReadOnlyDictionary<Guid, string?> parentEmails = invitationParents
            .ToDictionary(parent => parent.Id, parent => parent.User?.Email);

        Guid[] studentIds = relationshipList
            .Select(relationship => relationship.StudentId)
            .Distinct()
            .ToArray();

        IEnumerable<Student> invitationStudents = await studentRepository.GetAllAsync(
            studentItem => studentIds.Contains(studentItem.Id),
            studentItem => studentItem.User);

        IReadOnlyDictionary<Guid, string?> studentEmails = invitationStudents
            .ToDictionary(studentItem => studentItem.Id, studentItem => studentItem.User?.Email);

        List<InvitationResponse> responses = relationshipList
            .Select(relationship =>
            {
                bool isInitiator = relationship.InitiatedBy == InvitationInitiator.Parent
                    ? parentId == relationship.ParentId
                    : studentId == relationship.StudentId;

                string direction = isInitiator ? "Outgoing" : "Incoming";

                string? otherPartyEmail = parentId == relationship.ParentId
                    ? studentEmails.GetValueOrDefault(relationship.StudentId)
                    : parentEmails.GetValueOrDefault(relationship.ParentId);

                return relationship.ToResponse(direction, otherPartyEmail);
            })
            .ToList();

        if (studentId is Guid currentStudentId)
        {
            IEnumerable<StudentParentInvitation> outgoingInvitations =
                await invitationRepository.GetAllAsync(
                    invitation => invitation.StudentId == currentStudentId &&
                                  invitation.Status == ParentInvitationStatus.Pending,
                    invitation => invitation.Student,
                    invitation => invitation.Student.User);

            responses.AddRange(
                outgoingInvitations.Select(invitation =>
                    invitation.ToResponse(
                        "Outgoing",
                        null,
                        invitation.TargetEmailNormalized)));
        }

        if (parent is not null && !string.IsNullOrWhiteSpace(parent.User?.Email))
        {
            string normalizedParentEmail = parent.User.Email.Trim().ToUpperInvariant();

            IEnumerable<StudentParentInvitation> incomingInvitations =
                await invitationRepository.GetAllAsync(
                    invitation => invitation.Type == StudentParentInvitationType.Email &&
                                  invitation.TargetEmailNormalized == normalizedParentEmail &&
                                  invitation.Status == ParentInvitationStatus.Pending,
                    invitation => invitation.Student,
                    invitation => invitation.Student.User);

            responses.AddRange(
                incomingInvitations.Select(invitation =>
                    invitation.ToResponse(
                        "Incoming",
                        parent.Id,
                        invitation.Student.User?.Email)));
        }

        return responses;
    }
}