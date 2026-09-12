using FocusLens.Domain.StudySessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public sealed class StudySessionSelectionConfiguration : IEntityTypeConfiguration<StudySessionSelection>
{
    public void Configure(EntityTypeBuilder<StudySessionSelection> builder)
    {
        builder.ToTable("StudySessionSelections");
        builder.HasKey(selection => selection.Id);
        builder.Property(selection => selection.StudySessionId).IsRequired();
        builder.Property(selection => selection.StudyMaterialId).IsRequired();
        builder.Property(selection => selection.FromPage).IsRequired();
        builder.Property(selection => selection.ToPage).IsRequired();
        builder.Property(selection => selection.DerivedStorageReference).HasMaxLength(500);
        builder.HasIndex(selection => selection.StudySessionId).IsUnique();

        builder.HasOne(selection => selection.Session)
            .WithOne(session => session.Selection)
            .HasForeignKey<StudySessionSelection>(selection => selection.StudySessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<StudyMaterial>()
            .WithMany()
            .HasForeignKey(selection => selection.StudyMaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(selection => selection.SelectedSections)
            .WithOne()
            .HasForeignKey(section => section.StudySessionSelectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}