using System.Security.Cryptography;
using System.Text;
using FocusLens.Application.Access;
using FocusLens.Contracts.Access;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;

namespace FocusLens.Application.UnitTests.Access;

public sealed class ResolveStudentParentInvitationQueryHandlerTests
{
    private const string RawToken = "test-invitation-token";

    private static readonly DateTimeOffset Now = new(
        2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WithValidInvitation_ReturnsPrivacySafePreview()
    {
        Student student = CreateStudent(new DateOnly(2012, 5, 12));
        student.SetPreferredName("Youssef");

        StudentParentInvitation invitation = CreateInvitation(
            student,
            Now.AddDays(7));

        ResolveStudentParentInvitationQueryHandler handler = CreateHandler([invitation]);

        Result<ResolveStudentParentInvitationResponse> result = await handler.Handle(
            new ResolveStudentParentInvitationQuery(RawToken),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(invitation.Id, result.Value.InvitationId);
        Assert.Equal("Pending", result.Value.Status);
        Assert.Equal("Email", result.Value.Type);
        Assert.Equal("PARENT@EXAMPLE.COM", result.Value.TargetEmail);
        Assert.Equal("Youssef", result.Value.StudentPreferredName);
        Assert.Equal("13–15", result.Value.AgeRange);
        Assert.Equal(invitation.ExpiresAtUtc, result.Value.ExpiresAtUtc);
        Assert.True(result.Value.RequiresSignIn);
    }

    [Fact]
    public async Task Handle_WithLinkInvitation_ReturnsLinkTypeWithoutTargetEmail()
    {
        Student student = CreateStudent(new DateOnly(2012, 5, 12));
        StudentParentInvitation invitation = CreateInvitation(
            student,
            Now.AddDays(7),
            StudentParentInvitationType.Link,
            null);

        ResolveStudentParentInvitationQueryHandler handler = CreateHandler([invitation]);

        Result<ResolveStudentParentInvitationResponse> result = await handler.Handle(
            new ResolveStudentParentInvitationQuery(RawToken),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Link", result.Value.Type);
        Assert.Null(result.Value.TargetEmail);
    }

    [Theory]
    [InlineData(8, "8–10")]
    [InlineData(10, "8–10")]
    [InlineData(11, "11–12")]
    [InlineData(12, "11–12")]
    [InlineData(13, "13–15")]
    [InlineData(15, "13–15")]
    [InlineData(16, "16–18")]
    [InlineData(18, "16–18")]
    [InlineData(19, null)]
    public async Task Handle_WithDifferentStudentAges_ReturnsExpectedAgeRange(
        int age,
        string? expectedAgeRange)
    {
        Student student = CreateStudent(DateOfBirthForAge(age));
        StudentParentInvitation invitation = CreateInvitation(
            student,
            Now.AddDays(7));

        ResolveStudentParentInvitationQueryHandler handler = CreateHandler([invitation]);

        Result<ResolveStudentParentInvitationResponse> result = await handler.Handle(
            new ResolveStudentParentInvitationQuery(RawToken),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedAgeRange, result.Value.AgeRange);
    }

    [Fact]
    public async Task Handle_WithoutDateOfBirth_ReturnsNullAgeRange()
    {
        Student student = CreateStudent(null);
        StudentParentInvitation invitation = CreateInvitation(
            student,
            Now.AddDays(7));

        ResolveStudentParentInvitationQueryHandler handler = CreateHandler([invitation]);

        Result<ResolveStudentParentInvitationResponse> result = await handler.Handle(
            new ResolveStudentParentInvitationQuery(RawToken),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.AgeRange);
    }

    [Fact]
    public async Task Handle_WithEmptyToken_ReturnsInvalidInvitation()
    {
        ResolveStudentParentInvitationQueryHandler handler = CreateHandler();

        Result<ResolveStudentParentInvitationResponse> result = await handler.Handle(
            new ResolveStudentParentInvitationQuery(""),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "Access.InvitationInvalidOrExpired",
            result.Errors.Single().Code);
    }

    [Fact]
    public async Task Handle_WithUnknownToken_ReturnsInvalidInvitation()
    {
        ResolveStudentParentInvitationQueryHandler handler = CreateHandler();

        Result<ResolveStudentParentInvitationResponse> result = await handler.Handle(
            new ResolveStudentParentInvitationQuery("unknown-token"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "Access.InvitationInvalidOrExpired",
            result.Errors.Single().Code);
    }

    [Fact]
    public async Task Handle_WithExpiredInvitation_ReturnsInvalidInvitation()
    {
        Student student = CreateStudent(new DateOnly(2012, 5, 12));
        StudentParentInvitation invitation = CreateInvitation(
            student,
            Now.AddMinutes(-1));

        ResolveStudentParentInvitationQueryHandler handler = CreateHandler([invitation]);

        Result<ResolveStudentParentInvitationResponse> result = await handler.Handle(
            new ResolveStudentParentInvitationQuery(RawToken),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "Access.InvitationInvalidOrExpired",
            result.Errors.Single().Code);
    }

    [Fact]
    public async Task Handle_WithNonPendingInvitation_ReturnsConflict()
    {
        Student student = CreateStudent(new DateOnly(2012, 5, 12));
        StudentParentInvitation invitation = CreateInvitation(
            student,
            Now.AddDays(7));
        invitation.Decline(Now.AddMinutes(-1));

        ResolveStudentParentInvitationQueryHandler handler = CreateHandler([invitation]);

        Result<ResolveStudentParentInvitationResponse> result = await handler.Handle(
            new ResolveStudentParentInvitationQuery(RawToken),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "Access.InvitationNoLongerPending",
            result.Errors.Single().Code);
    }

    private static ResolveStudentParentInvitationQueryHandler CreateHandler(
        StudentParentInvitation[]? invitations = null,
        ParentStudentRelationship[]? relationships = null)
    {
        return new ResolveStudentParentInvitationQueryHandler(
            new InMemoryRepository<StudentParentInvitation>(invitations ?? []),
            new InMemoryRepository<ParentStudentRelationship>(relationships ?? []),
            new FixedTimeProvider(Now));
    }

    [Fact]
    public async Task Handle_WithParentToStudentRelationshipToken_ReturnsPrivacySafePreview()
    {
        Student student = CreateStudent(new DateOnly(2012, 5, 12));
        student.SetPreferredName("Youssef");
        student.SetPrivateProperty(
            "User",
            new ApplicationUser { FirstName = "Youssef", Email = "student@example.com" });

        Parent parent = new(Guid.NewGuid());
        ParentStudentRelationship relationship = new(
            parent.Id,
            student.Id,
            InvitationInitiator.Parent,
            Now.AddDays(7));
        relationship.SetPrivateProperty("Student", student);
        relationship.SetInvitationTokenHash(Hash(RawToken));

        ResolveStudentParentInvitationQueryHandler handler = CreateHandler(
            relationships: [relationship]);

        Result<ResolveStudentParentInvitationResponse> result = await handler.Handle(
            new ResolveStudentParentInvitationQuery(RawToken),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(relationship.Id, result.Value.InvitationId);
        Assert.Equal("Pending", result.Value.Status);
        Assert.Equal("Relationship", result.Value.Type);
        Assert.Equal("student@example.com", result.Value.TargetEmail);
        Assert.Equal("Youssef", result.Value.StudentPreferredName);
        Assert.Equal("13–15", result.Value.AgeRange);
        Assert.Equal(relationship.ExpiresAtUtc, result.Value.ExpiresAtUtc);
        Assert.True(result.Value.RequiresSignIn);
    }

    private static StudentParentInvitation CreateInvitation(
        Student student,
        DateTimeOffset expiresAtUtc,
        StudentParentInvitationType type = StudentParentInvitationType.Email,
        string? targetEmailNormalized = "PARENT@EXAMPLE.COM")
    {
        string tokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(RawToken)));

        StudentParentInvitation invitation = new(
            student.Id,
            type,
            targetEmailNormalized,
            tokenHash,
            expiresAtUtc);

        invitation.SetPrivateProperty("Student", student);
        return invitation;
    }

    private static Student CreateStudent(DateOnly? dateOfBirth)
    {
        Guid userId = Guid.NewGuid();
        Student student = new(userId);
        student.SetPreferredName("Youssef");
        student.SetDateOfBirth(dateOfBirth);
        student.SetPrivateProperty(
            "User",
            new ApplicationUser { FirstName = "Youssef", LastName = "Ali" });

        return student;
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static DateOnly DateOfBirthForAge(int age) => new(Now.Year - age, 1, 1);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}