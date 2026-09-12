using FocusLens.Domain.StudySessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public sealed class StudySessionImageConfiguration : IEntityTypeConfiguration<StudySessionImage>
{
    public void Configure(EntityTypeBuilder<StudySessionImage> builder)
    {
        builder.ToTable("StudySessionImages");
        builder.HasKey(image => image.Id);
        builder.Property(image => image.StudySessionId).IsRequired();
        builder.Property(image => image.OriginalFileName).HasMaxLength(260).IsRequired();
        builder.Property(image => image.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(image => image.FileSizeBytes).IsRequired();
        builder.Property(image => image.StorageReference).HasMaxLength(500).IsRequired();

        builder.HasIndex(image => image.StudySessionId);
        builder.HasOne<StudySession>()
            .WithMany()
            .HasForeignKey(image => image.StudySessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
