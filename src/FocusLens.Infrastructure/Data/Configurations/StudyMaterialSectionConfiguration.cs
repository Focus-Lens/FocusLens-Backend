using FocusLens.Domain.StudySessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public sealed class StudyMaterialSectionConfiguration : IEntityTypeConfiguration<StudyMaterialSection>
{
    public void Configure(EntityTypeBuilder<StudyMaterialSection> builder)
    {
        builder.ToTable("StudyMaterialSections");
        builder.HasKey(section => section.Id);
        builder.Property(section => section.StudyMaterialId).IsRequired();
        builder.Property(section => section.Name).HasMaxLength(200).IsRequired();
        builder.Property(section => section.EstimatedDurationMinutes).IsRequired();
        builder.Property(section => section.FromPage).IsRequired();
        builder.Property(section => section.ToPage).IsRequired();
        builder.HasIndex(section => section.StudyMaterialId);
        builder.HasOne<StudyMaterial>()
            .WithMany()
            .HasForeignKey(section => section.StudyMaterialId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}