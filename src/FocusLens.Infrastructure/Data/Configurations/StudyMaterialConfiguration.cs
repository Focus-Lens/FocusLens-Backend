using FocusLens.Domain;
using FocusLens.Domain.StudySessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public sealed class StudyMaterialConfiguration : IEntityTypeConfiguration<StudyMaterial>
{
    public void Configure(EntityTypeBuilder<StudyMaterial> builder)
    {
        builder.ToTable("StudyMaterials");
        builder.HasKey(material => material.Id);
        builder.Property(material => material.StudentId).IsRequired();
        builder.Property(material => material.FileName).HasMaxLength(260).IsRequired();
        builder.Property(material => material.FileSizeBytes).IsRequired();
        builder.Property(material => material.PageCount).IsRequired();
        builder.Property(material => material.StorageReference).HasMaxLength(500).IsRequired();
        builder.Property(material => material.Source).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.HasIndex(material => material.StudentId);
        builder.HasOne<Student>()
            .WithMany()
            .HasForeignKey(material => material.StudentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}