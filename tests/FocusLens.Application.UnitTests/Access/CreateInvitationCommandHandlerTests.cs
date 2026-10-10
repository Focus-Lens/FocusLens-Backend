using System.Security.Cryptography;
using System.Text;
using FocusLens.Application.Access;
using FocusLens.Contracts.Access;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;

namespace FocusLens.Application.UnitTests.Access;

public class CreateInvitationCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithValidParentAndStudent_CreatesPendingInvitation()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        Student student = new(studentUserId);
        ApplicationUser studentUser = new() { Email = "student@example.com" };
        student.SetPrivateProperty("User", studentUser);

        InMemoryRepository<Parent> parentRepository = new(parent);
        InMemoryRepository<Student> studentRepository = new(student);
        InMemoryRepository<ParentStudentRelationship> relationshipRepository = new();
        const string parentEmail = "parent@example.com";
        FakeCurrentUser currentUser = new(parentUserId, parentEmail);
        FakeUnitOfWork unitOfWork = new();
        FakeEmailSender emailSender = new();

        CreateInvitationCommandHandler handler = new(
            parentRepository,
            studentRepository,
            relationshipRepository,
            currentUser,
            unitOfWork,
            emailSender,
            new FakeInvitationUrlBuilder());

        Result<InvitationResponse> result = await handler.Handle(
            new CreateInvitationCommand(
                new CreateInvitationRequest("student@example.com")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(parent.Id, result.Value.ParentId);
        Assert.Equal(student.Id, result.Value.StudentId);
        Assert.Equal("Pending", result.Value.Status);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
        Assert.Single(emailSender.Invitations);
        Assert.Equal("student@example.com", emailSender.Invitations[0].StudentEmail);
        Assert.Equal(parentEmail, emailSender.Invitations[0].ParentEmail);
        string invitationUrl = emailSender.Invitations[0].InvitationUrl;
        string token = invitationUrl[(invitationUrl.LastIndexOf('/') + 1)..];
        Assert.Equal(64, token.Length);
        ParentStudentRelationship savedRelationship =
            (await relationshipRepository.GetAllAsync()).Single();
        Assert.Equal(
            Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(token))),
            savedRelationship.InvitationTokenHash);
    }

    [Fact]
    public async Task Handle_WhenStudentDoesNotExist_ReturnsNotFound()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);

        CreateInvitationCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<Student>(),
            new InMemoryRepository<ParentStudentRelationship>(),
            new FakeCurrentUser(parentUserId, "parent@example.com"),
            new FakeUnitOfWork(),
            new FakeEmailSender(),
            new FakeInvitationUrlBuilder());

        Result<InvitationResponse> result = await handler.Handle(
            new CreateInvitationCommand(
                new CreateInvitationRequest("missing@example.com")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.TopError.Type);
        Assert.Equal("Access.StudentNotFound", result.TopError.Code);
    }

    [Fact]
    public async Task Handle_WithPendingRelationship_ResendsInvitation()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        Student student = new(studentUserId);
        ApplicationUser studentUser = new() { Email = "student@example.com" };
        student.SetPrivateProperty("User", studentUser);

        ParentStudentRelationship existingRelationship = new(
            parent.Id,
            student.Id,
            InvitationInitiator.Parent,
            DateTimeOffset.UtcNow.AddDays(7));

        FakeUnitOfWork unitOfWork = new();
        FakeEmailSender emailSender = new();

        CreateInvitationCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(existingRelationship),
            new FakeCurrentUser(parentUserId, "parent@example.com"),
            unitOfWork,
            emailSender,
            new FakeInvitationUrlBuilder());

        Result<InvitationResponse> result = await handler.Handle(
            new CreateInvitationCommand(
                new CreateInvitationRequest("student@example.com")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Pending", result.Value.Status);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
        Assert.Single(emailSender.Invitations);
        Assert.Equal("student@example.com", emailSender.Invitations[0].StudentEmail);
        Assert.Equal("parent@example.com", emailSender.Invitations[0].ParentEmail);
        Assert.StartsWith("https://student.focuslens.test/invitations/", emailSender.Invitations[0].InvitationUrl);
        Assert.False(string.IsNullOrWhiteSpace(existingRelationship.InvitationTokenHash));
    }

    [Fact]
    public async Task Handle_WithExpiredPendingRelationship_RenewsInvitation()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Parent parent = new(parentUserId);
        Student student = new(studentUserId);
        student.SetPrivateProperty(
            "User",
            new ApplicationUser { Email = "student@example.com" });

        ParentStudentRelationship existingRelationship = new(
            parent.Id,
            student.Id,
            InvitationInitiator.Parent,
            now.AddMinutes(-1));

        FakeUnitOfWork unitOfWork = new();
        FakeEmailSender emailSender = new();
        CreateInvitationCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(existingRelationship),
            new FakeCurrentUser(parentUserId, "parent@example.com"),
            unitOfWork,
            emailSender,
            new FakeInvitationUrlBuilder(),
            new FixedTimeProvider(now));

        Result<InvitationResponse> result = await handler.Handle(
            new CreateInvitationCommand(
                new CreateInvitationRequest("student@example.com")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(RelationshipStatus.Pending, existingRelationship.Status);
        Assert.Equal(now.AddDays(7), existingRelationship.ExpiresAtUtc);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
        Assert.Single(emailSender.Invitations);
        Assert.StartsWith(
            "https://student.focuslens.test/invitations/",
            emailSender.Invitations[0].InvitationUrl);
    }

    [Fact]
    public async Task Handle_WithEmptyEmail_ReturnsValidationError()
    {
        CreateInvitationCommandHandler handler = new(
            new InMemoryRepository<Parent>(),
            new InMemoryRepository<Student>(),
            new InMemoryRepository<ParentStudentRelationship>(),
            new FakeCurrentUser(Guid.NewGuid()),
            new FakeUnitOfWork(),
            new FakeEmailSender(),
            new FakeInvitationUrlBuilder());

        Result<InvitationResponse> result = await handler.Handle(
            new CreateInvitationCommand(
                new CreateInvitationRequest("   ")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Validation, result.TopError.Type);
        Assert.Equal("Access.StudentEmailRequired", result.TopError.Code);
    }


    [Fact]
    public async Task Handle_WhenExistingRelationshipIsRevoked_ReinvitesStudent()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        Student student = new(studentUserId);

        ApplicationUser studentUser = new() { Email = "student@example.com" };

        student.SetPrivateProperty("User", studentUser);

        ParentStudentRelationship existingRelationship =
            new(parent.Id, student.Id);

        existingRelationship.Reject();

        InMemoryRepository<ParentStudentRelationship> relationshipRepository =
            new(
                existingRelationship);

        FakeUnitOfWork unitOfWork = new();

        CreateInvitationCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<Student>(student),
            relationshipRepository,
            new FakeCurrentUser(parentUserId, "parent@example.com"),
            unitOfWork,
            new FakeEmailSender(),
            new FakeInvitationUrlBuilder());

        Result<InvitationResponse> result = await handler.Handle(
            new CreateInvitationCommand(
                new CreateInvitationRequest("student@example.com")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(existingRelationship.Id, result.Value.Id);
        Assert.Equal("Pending", result.Value.Status);
        Assert.Null(result.Value.RevokedAtUtc);
        Assert.Equal(RelationshipStatus.Pending, existingRelationship.Status);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}