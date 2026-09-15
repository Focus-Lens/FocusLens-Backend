using FocusLens.Domain.StudySessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public sealed class StudySessionBehaviorAnalysisJobConfiguration
    : IEntityTypeConfiguration<StudySessionBehaviorAnalysisJob>
{
    public void Configure(EntityTypeBuilder<StudySessionBehaviorAnalysisJob> builder)
    {
        builder.ToTable("StudySessionBehaviorAnalysisJobs");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.StudySessionId)
            .IsRequired();

        builder.Property(item => item.Status)
            .IsRequired();

        builder.Property(item => item.AttemptCount)
            .IsRequired();

        builder.Property(item => item.CreatedAtUtc)
            .IsRequired();

        builder.Property(item => item.LastError)
            .HasMaxLength(2000);

        builder.HasIndex(item => item.StudySessionId)
            .IsUnique();

        builder.HasIndex(item => new
        {
            item.Status,
            item.NextAttemptAtUtc
        });

        builder.HasOne<StudySession>()
            .WithMany()
            .HasForeignKey(item => item.StudySessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
