using FocusLens.Domain.StudySessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public sealed class StudySessionBehaviorWindowConfiguration
    : IEntityTypeConfiguration<StudySessionBehaviorWindow>
{
    public void Configure(EntityTypeBuilder<StudySessionBehaviorWindow> builder)
    {
        builder.ToTable("StudySessionBehaviorWindows");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.StudySessionId).IsRequired();
        builder.Property(item => item.WindowIndex).IsRequired();
        builder.Property(item => item.WindowStartUtc).IsRequired();
        builder.Property(item => item.WindowEndUtc).IsRequired();
        builder.Property(item => item.WindowActiveTimeSeconds).HasColumnType("float");
        builder.Property(item => item.FocusState).HasMaxLength(50);
        builder.Property(item => item.FocusTrend).HasMaxLength(20);
        builder.Property(item => item.UnderstandingTrend).HasMaxLength(20);
        builder.Property(item => item.RawAction).HasMaxLength(50);
        builder.Property(item => item.RecommendedAction).HasMaxLength(50);
        builder.HasIndex(item => new { item.StudySessionId, item.WindowIndex }).IsUnique();

        builder.HasOne<StudySession>().WithMany().HasForeignKey(item => item.StudySessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}