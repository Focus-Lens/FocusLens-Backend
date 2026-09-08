using FocusLens.Domain.StudySessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public sealed class StudySessionConfiguration : IEntityTypeConfiguration<StudySession>
{
    public void Configure(EntityTypeBuilder<StudySession> builder)
    {
        builder.ToTable("StudySessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.StudentId).IsRequired();
        builder.Property(session => session.Mode).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(session => session.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(session => session.StartedAtUtc);
        builder.Property(session => session.FocusDurationMinutes);
        builder.Property(session => session.SelectedSubjectId);
        builder.Property(session => session.StudyMaterialId);

        builder.HasIndex(session => new { session.StudentId, session.Status });

        builder.HasOne<FocusLens.Domain.Student>()
            .WithMany()
            .HasForeignKey(session => session.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(session => session.Material)
            .WithMany()
            .HasForeignKey(session => session.StudyMaterialId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
