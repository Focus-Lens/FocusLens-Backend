using FocusLens.Application.Access;
using FocusLens.Contracts.Access;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;

namespace FocusLens.Application.UnitTests.Access;

public sealed class StudentParentInvitationCommandHandlerTests
{
    [Fact]
    public async Task Create_WithStudentAndValidEmail_CreatesAndEmailsInvitation()
    {
        Guid studentUserId = Guid.NewGuid();
        Student student = new(studentUserId);
        student.SetPreferredName("Youssef");
        student.SetPrivateProperty("User", new ApplicationUser { FirstName = "Youssef", LastName = "Ali" });

        InMemoryRepository<StudentParentInvitation> invitations = new();
        FakeEmailSender emailSender = new();
        CreateStudentParentInvitationCommandHandler handler = new(
            new InMemoryRepository<Student>(student),
            invitations,
            new FakeCurrentUser(studentUserId),
            new FakeUnitOfWork(),
            emailSender,
            new FakeInvitationUrlBuilder(),
            TimeProvider.System);

        Result<InvitationResponse> result = await handler.Handle(
            new CreateStudentParentInvitationCommand(
                new CreateInvitationRequest("parent@example.com")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Pending", result.Value.Status);
        Assert.NotNull(result.Value.InvitationUrl);
        Assert.Equal(
            StudentParentInvitationType.Email,
            (await invitations.GetAllAsync()).Single().Type);
        Assert.Contains("/invitations/parent/", result.Value.InvitationUrl);
        Assert.Single(emailSender.StudentParentInvitations);
        Assert.Equal("parent@example.com", emailSender.StudentParentInvitations[0].ParentEmail);
        Assert.Equal("Youssef", emailSender.StudentParentInvitations[0].StudentDisplayName);
    }
}