using FocusLens.Domain.ChildSetup;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public sealed class ChildSetupInvitationConfiguration
    : IEntityTypeConfiguration<ChildSetupInvitation>
{
    public void Configure(EntityTypeBuilder<ChildSetupInvitation> builder)
    {
        builder.ToTable("ChildSetupInvitations");

        builder.HasKey(invitation => invitation.Id);

        builder.Property(invitation => invitation.ChildSetupDraftId)
            .IsRequired();

        builder.Property(invitation => invitation.TargetEmailNormalized)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(invitation => invitation.TokenHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(invitation => invitation.ExpiresAtUtc)
            .IsRequired();

        builder.Property(invitation => invitation.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(invitation => invitation.ClaimedAtUtc)
            .IsRequired(false);

        builder.HasIndex(invitation => invitation.TokenHash)
            .IsUnique();

        builder.HasIndex(invitation => new
        {
            invitation.ChildSetupDraftId, invitation.TargetEmailNormalized, invitation.Status
        });

        builder.HasOne(invitation => invitation.ChildSetupDraft)
            .WithMany()
            .HasForeignKey(invitation => invitation.ChildSetupDraftId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}