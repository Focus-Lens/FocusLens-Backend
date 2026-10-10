using FocusLens.Domain.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public class ParentStudentRelationshipConfiguration
    : IEntityTypeConfiguration<ParentStudentRelationship>
{
    public void Configure(EntityTypeBuilder<ParentStudentRelationship> builder)
    {
        builder.ToTable("ParentStudentRelationships");

        builder.HasKey(relationship => relationship.Id);

        builder.Property(relationship => relationship.ParentId).IsRequired();

        builder.Property(relationship => relationship.StudentId).IsRequired();

        builder.Property(relationship => relationship.Status).IsRequired();

        builder.Property(relationship => relationship.InitiatedBy).IsRequired();

        builder.Property(relationship => relationship.ExpiresAtUtc).IsRequired(false);

        builder.Property(relationship => relationship.InvitationTokenHash)
            .HasMaxLength(128)
            .IsRequired(false);

        builder.Property(relationship => relationship.RevokedAtUtc).IsRequired(false);

        builder.Property(relationship => relationship.SelectedOverviewWeekStart).IsRequired(false);

        builder
            .HasIndex(relationship => new { relationship.ParentId, relationship.StudentId })
            .IsUnique();

        builder
            .HasIndex(relationship => relationship.InvitationTokenHash)
            .IsUnique()
            .HasFilter("[InvitationTokenHash] IS NOT NULL");

        builder
            .HasOne(relationship => relationship.Parent)
            .WithMany()
            .HasForeignKey(relationship => relationship.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(relationship => relationship.Student)
            .WithMany()
            .HasForeignKey(relationship => relationship.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}