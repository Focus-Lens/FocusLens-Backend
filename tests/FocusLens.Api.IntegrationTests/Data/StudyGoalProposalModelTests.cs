using FocusLens.Domain;
using FocusLens.Domain.Students;
using FocusLens.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FocusLens.Api.IntegrationTests.Data;

public sealed class StudyGoalProposalModelTests
{
    [Fact]
    public void StudyGoalProposal_UsesStudentCascadeAndParentNoAction()
    {
        DbContextOptions<ApplicationDbContext> options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"StudyGoalProposalModel-{Guid.NewGuid()}")
                .Options;

        using ApplicationDbContext dbContext = new(options);

        IEntityType entityType = dbContext.Model.FindEntityType(typeof(StudyGoalProposal))!;

        Assert.Equal(
            DeleteBehavior.NoAction,
            GetForeignKey(entityType, typeof(Parent), nameof(StudyGoalProposal.ParentId))
                .DeleteBehavior);
        Assert.Equal(
            DeleteBehavior.Cascade,
            GetForeignKey(entityType, typeof(Student), nameof(StudyGoalProposal.StudentId))
                .DeleteBehavior);
    }

    private static IForeignKey GetForeignKey(
        IEntityType entityType,
        Type principalType,
        string propertyName)
    {
        return entityType.GetForeignKeys().Single(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == principalType &&
            foreignKey.Properties.Single().Name == propertyName);
    }
}
