using FocusLens.Domain.StudySessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public sealed class StudySessionCompletedSectionConfiguration
    : IEntityTypeConfiguration<StudySessionCompletedSection>
{
    public void Configure(EntityTypeBuilder<StudySessionCompletedSection> builder)
    {
        builder.ToTable("StudySessionCompletedSections");
        builder.HasKey(section => section.Id);
        builder.Property(section => section.Id).ValueGeneratedNever();
        builder.Property(section => section.Id).ValueGeneratedNever();
        builder.Property(section => section.StudySessionId).IsRequired();
        builder.Property(section => section.StudyMaterialSectionId).IsRequired();
        builder.HasIndex(section => new { section.StudySessionId, section.StudyMaterialSectionId }).IsUnique();
        builder.HasOne<StudyMaterialSection>()
            .WithMany()
            .HasForeignKey(section => section.StudyMaterialSectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
