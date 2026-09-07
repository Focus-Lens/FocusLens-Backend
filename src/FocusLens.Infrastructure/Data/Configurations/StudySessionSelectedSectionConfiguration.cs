using FocusLens.Domain.StudySessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public sealed class StudySessionSelectedSectionConfiguration : IEntityTypeConfiguration<StudySessionSelectedSection>
{
    public void Configure(EntityTypeBuilder<StudySessionSelectedSection> builder)
    {
        builder.ToTable("StudySessionSelectedSections");
        builder.HasKey(section => section.Id);
        builder.Property(section => section.StudySessionId).IsRequired();
        builder.Property(section => section.StudyMaterialSectionId).IsRequired();
        builder.Property(section => section.EstimatedDurationMinutes).IsRequired();
        builder.HasIndex(section => new { section.StudySessionId, section.StudyMaterialSectionId }).IsUnique();
        builder.HasOne<StudyMaterialSection>()
            .WithMany()
            .HasForeignKey(section => section.StudyMaterialSectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
