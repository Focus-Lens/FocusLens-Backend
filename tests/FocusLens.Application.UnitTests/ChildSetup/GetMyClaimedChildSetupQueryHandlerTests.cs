using FocusLens.Application.ChildSetup;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Application.UnitTests.ChildSetup;

public sealed class GetMyClaimedChildSetupQueryHandlerTests
{
    [Fact]
    public async Task GetMyClaimedChildSetup_IgnoresPreviouslyActivatedDraft()
    {
        Guid userId = Guid.NewGuid();
        Student student = new(userId);
        ChildSetupDraft previouslyActivatedDraft = new(Guid.NewGuid());
        previouslyActivatedDraft.MarkInvited();
        previouslyActivatedDraft.MarkClaimed(student.Id);
        previouslyActivatedDraft.MarkActivated();

        ChildSetupDraft currentClaimedDraft = new(Guid.NewGuid());
        currentClaimedDraft.MarkInvited();
        currentClaimedDraft.MarkClaimed(student.Id);

        GetMyClaimedChildSetupQueryHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ChildSetupDraft>(previouslyActivatedDraft, currentClaimedDraft),
            new InMemoryRepository<Parent>(),
            new FakeCurrentUser(userId));

        Result<FocusLens.Contracts.ChildSetup.ChildSetupDraftResponse> result = await handler.Handle(
            new GetMyClaimedChildSetupQuery(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(currentClaimedDraft.Id, result.Value.Id);
        Assert.Equal("Claimed", result.Value.Status);
    }
}
