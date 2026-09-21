using FocusLens.Application.Access;
using FocusLens.Contracts.Access;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Identity;

namespace FocusLens.Application.UnitTests.Access;

public class GetMyInvitationsQueryHandlerTests
{
    [Fact]
    public async Task Handle_AsParent_ReturnsMyInvitations()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        parent.SetPrivateProperty("User", new ApplicationUser { Email = "parent@example.com" });
        Student student = new(studentUserId);
        student.SetPrivateProperty("User", new ApplicationUser { Email = "student@example.com" });
        ParentStudentRelationship relationship = new(parent.Id, student.Id);

        GetMyInvitationsQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<StudentParentInvitation>(),
            new FakeCurrentUser(parentUserId)
        );

        IReadOnlyList<InvitationResponse> result = await handler.Handle(
            new GetMyInvitationsQuery(),
            CancellationToken.None
        );

        Assert.Single(result);
        Assert.Equal(relationship.Id, result[0].Id);
        Assert.Equal(parent.Id, result[0].ParentId);
        Assert.Single(result);
        Assert.Equal(student.Id, result[0].StudentId);
        Assert.Equal("student@example.com", result[0].OtherPartyEmail);
        Assert.Equal("Outgoing", result[0].Direction);
        Assert.Equal("Relationship", result[0].Kind);
        Assert.Equal("Pending", result[0].Status);
    }

    [Fact]
    public async Task Handle_AsStudent_ReturnsReceivedInvitations()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        parent.SetPrivateProperty("User", new ApplicationUser { Email = "parent@example.com" });
        Student student = new(studentUserId);
        student.SetPrivateProperty("User", new ApplicationUser { Email = "student@example.com" });
        ParentStudentRelationship relationship = new(parent.Id, student.Id);

        GetMyInvitationsQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<StudentParentInvitation>(),
            new FakeCurrentUser(studentUserId)
        );

        IReadOnlyList<InvitationResponse> result = await handler.Handle(
            new GetMyInvitationsQuery(),
            CancellationToken.None
        );

        Assert.Single(result);
        Assert.Equal(relationship.Id, result[0].Id);
        Assert.Equal(parent.Id, result[0].ParentId);
        Assert.Single(result);
        Assert.Equal(student.Id, result[0].StudentId);
        Assert.Equal("parent@example.com", result[0].OtherPartyEmail);
        Assert.Equal("Incoming", result[0].Direction);
        Assert.Equal("Relationship", result[0].Kind);
    }

    [Fact]
    public async Task Handle_WithUnrelatedUser_ReturnsEmptyList()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();
        Guid otherUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        Student student = new(studentUserId);
        student.SetPrivateProperty("User", new ApplicationUser { Email = "student@example.com" });
        ParentStudentRelationship relationship = new(parent.Id, student.Id);

        GetMyInvitationsQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<StudentParentInvitation>(),
            new FakeCurrentUser(otherUserId)
        );

        IReadOnlyList<InvitationResponse> result = await handler.Handle(
            new GetMyInvitationsQuery(),
            CancellationToken.None
        );

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_WithEmptyCurrentUser_ReturnsEmptyList()
    {
        Parent parent = new(Guid.NewGuid());
        Student student = new(Guid.NewGuid());
        ParentStudentRelationship relationship = new(parent.Id, student.Id);

        GetMyInvitationsQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<StudentParentInvitation>(),
            new FakeCurrentUser(Guid.Empty)
        );

        IReadOnlyList<InvitationResponse> result = await handler.Handle(
            new GetMyInvitationsQuery(),
            CancellationToken.None
        );

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_AsStudent_IncludesPendingStudentParentInvitation()
    {
        Guid studentUserId = Guid.NewGuid();

        Student student = new(studentUserId);
        student.SetPrivateProperty("User", new ApplicationUser { Email = "student@example.com" });

        StudentParentInvitation invitation = new(
            student.Id,
            "PARENT@EXAMPLE.COM",
            "token-hash",
            DateTimeOffset.UtcNow.AddDays(7)
        );
        invitation.SetPrivateProperty("Student", student);

        GetMyInvitationsQueryHandler handler = new(
            new InMemoryRepository<Parent>(),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(),
            new InMemoryRepository<StudentParentInvitation>(invitation),
            new FakeCurrentUser(studentUserId)
        );

        IReadOnlyList<InvitationResponse> result = await handler.Handle(
            new GetMyInvitationsQuery(),
            CancellationToken.None
        );

        Assert.Single(result);
        Assert.Equal(invitation.Id, result[0].Id);
        Assert.Null(result[0].ParentId);
        Assert.Equal(student.Id, result[0].StudentId);
        Assert.Equal("Pending", result[0].Status);
        Assert.Equal("Student", result[0].InitiatedBy);
        Assert.Equal("Outgoing", result[0].Direction);
        Assert.Equal("StudentParent", result[0].Kind);
        Assert.Equal("PARENT@EXAMPLE.COM", result[0].OtherPartyEmail);
    }

    [Fact]
    public async Task Handle_AsParent_IncludesPendingStudentParentInvitationAddressedToParent()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        parent.SetPrivateProperty("User", new ApplicationUser { Email = "parent@example.com" });

        Student student = new(studentUserId);
        student.SetPrivateProperty("User", new ApplicationUser { Email = "student@example.com" });

        StudentParentInvitation invitation = new(
            student.Id,
            "PARENT@EXAMPLE.COM",
            "token-hash",
            DateTimeOffset.UtcNow.AddDays(7)
        );
        invitation.SetPrivateProperty("Student", student);

        GetMyInvitationsQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(),
            new InMemoryRepository<StudentParentInvitation>(invitation),
            new FakeCurrentUser(parentUserId)
        );

        IReadOnlyList<InvitationResponse> result = await handler.Handle(
            new GetMyInvitationsQuery(),
            CancellationToken.None
        );

        Assert.Single(result);
        Assert.Equal(student.Id, result[0].StudentId);
        Assert.Equal("Pending", result[0].Status);
        Assert.Equal("Student", result[0].InitiatedBy);
        Assert.Equal("Incoming", result[0].Direction);
        Assert.Equal("StudentParent", result[0].Kind);
        Assert.Equal("student@example.com", result[0].OtherPartyEmail);
    }
}
