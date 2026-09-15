using FocusLens.Domain.StudySessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public sealed class StudySessionBehaviorEventConfiguration
    : IEntityTypeConfiguration<StudySessionBehaviorEvent>
{
    public void Configure(EntityTypeBuilder<StudySessionBehaviorEvent> builder)
    {
        builder.ToTable("StudySessionBehaviorEvents");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.StudySessionId).IsRequired();
        builder.Property(item => item.EventType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(item => item.OccurredAtUtc).IsRequired();
        builder.HasIndex(item => new { item.StudySessionId, item.OccurredAtUtc });

        builder.HasOne<StudySession>().WithMany().HasForeignKey(item => item.StudySessionId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<StudyMaterialSection>().WithMany().HasForeignKey(item => item.StudyMaterialSectionId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<StudySessionQuestion>().WithMany().HasForeignKey(item => item.StudySessionQuestionId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
