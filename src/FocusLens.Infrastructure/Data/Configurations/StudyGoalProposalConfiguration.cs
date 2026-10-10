using FocusLens.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public sealed class StudyGoalProposalConfiguration : IEntityTypeConfiguration<StudyGoalProposal>
{
    public void Configure(EntityTypeBuilder<StudyGoalProposal> builder)
    {
        builder.ToTable("StudyGoalProposals");

        builder.HasKey(proposal => proposal.Id);

        builder.Property(proposal => proposal.ParentId)
            .IsRequired();

        builder.Property(proposal => proposal.StudentId)
            .IsRequired();

        builder.OwnsOne(proposal => proposal.Goal, goalBuilder =>
        {
            goalBuilder.Property(goal => goal.Period)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasColumnName("Period")
                .IsRequired();

            goalBuilder.Property(goal => goal.TargetMinutes)
                .HasColumnName("TargetMinutes")
                .IsRequired();

            goalBuilder.Property(goal => goal.Days)
                .HasConversion(
                    StudyTimeGoalDaysConverter.Converter,
                    StudyTimeGoalDaysConverter.Comparer)
                .HasColumnName("Days");

            goalBuilder.Property(goal => goal.StartDate)
                .HasColumnName("StartDate");
        });

        builder.Property(proposal => proposal.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(proposal => proposal.RespondedAtUtc)
            .IsRequired(false);

        builder.HasIndex(proposal => proposal.ParentId);

        builder.HasIndex(proposal => proposal.StudentId);

        builder.HasIndex(proposal => proposal.StudentId)
            .IsUnique()
            .HasFilter("[Status] = 'Pending'");

        builder.HasOne(proposal => proposal.Parent)
            .WithMany()
            .HasForeignKey(proposal => proposal.ParentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(proposal => proposal.Student)
            .WithMany()
            .HasForeignKey(proposal => proposal.StudentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}