using FocusLens.Domain.StudySessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace FocusLens.Infrastructure.Data.Configurations;
public sealed class StudySessionPauseIntervalConfiguration : IEntityTypeConfiguration<StudySessionPauseInterval>
{
 public void Configure(EntityTypeBuilder<StudySessionPauseInterval> builder)
 {
  builder.ToTable("StudySessionPauseIntervals"); builder.HasKey(x=>x.Id);
  builder.Property(x=>x.StudySessionId).IsRequired(); builder.Property(x=>x.StartedAtUtc).IsRequired();
  builder.HasIndex(x=>new { x.StudySessionId,x.StartedAtUtc });
  builder.HasOne<StudySession>().WithMany(x=>x.PauseIntervals).HasForeignKey(x=>x.StudySessionId).OnDelete(DeleteBehavior.Cascade);
 }
}
