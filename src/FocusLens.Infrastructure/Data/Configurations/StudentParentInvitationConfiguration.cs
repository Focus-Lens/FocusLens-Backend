using FocusLens.Domain.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public sealed class StudentParentInvitationConfiguration
    : IEntityTypeConfiguration<StudentParentInvitation>
{
    public void Configure(EntityTypeBuilder<StudentParentInvitation> builder)
    {
        builder.ToTable("StudentParentInvitations");
        builder.HasKey(invitation => invitation.Id);

        builder.Property(invitation => invitation.StudentId).IsRequired();
        builder.Property(invitation => invitation.TargetEmailNormalized)
            .HasMaxLength(256)
            .IsRequired();
        builder.Property(invitation => invitation.TokenHash)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(invitation => invitation.ExpiresAtUtc).IsRequired();
        builder.Property(invitation => invitation.Status).IsRequired();
        builder.Property(invitation => invitation.RespondedAtUtc).IsRequired(false);

        builder.HasIndex(invitation => invitation.TokenHash).IsUnique();
        builder.HasIndex(invitation => new
        {
            invitation.StudentId, invitation.TargetEmailNormalized, invitation.Status
        });

        builder.HasOne(invitation => invitation.Student)
            .WithMany()
            .HasForeignKey(invitation => invitation.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}