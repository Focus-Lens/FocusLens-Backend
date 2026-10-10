using FocusLens.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public sealed class StudentWeekConfiguration : IEntityTypeConfiguration<StudentWeek>
{
    public void Configure(EntityTypeBuilder<StudentWeek> builder)
    {
        builder.ToTable("StudentWeeks");
        builder.HasKey(week => week.Id);
        builder.Property(week => week.StudentId).IsRequired();
        builder.Property(week => week.StartsOn).HasColumnType("date").IsRequired();
        builder.Property(week => week.EndsOn).HasColumnType("date").IsRequired();
        builder.HasIndex(week => new { week.StudentId, week.StartsOn }).IsUnique();
        builder.HasOne(week => week.Student).WithMany(student => student.Weeks)
            .HasForeignKey(week => week.StudentId).OnDelete(DeleteBehavior.Cascade);
    }
}