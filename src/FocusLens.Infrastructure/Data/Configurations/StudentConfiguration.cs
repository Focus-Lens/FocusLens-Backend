using FocusLens.Domain;
using FocusLens.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students");

        builder.HasKey(student => student.Id);

        builder.Property(student => student.UserId)
            .IsRequired();

        builder.HasIndex(student => student.UserId)
            .IsUnique();

        builder.HasOne(student => student.User)
            .WithOne()
            .HasForeignKey<Student>(student => student.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(student => student.Goal)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(student => student.Grade)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(student => student.IsOnboardingCompleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.OwnsMany(student => student.Subjects, subjectBuilder =>
        {
            subjectBuilder.ToTable("StudentSubjects");

            subjectBuilder.HasKey(subject => subject.Id);

            // StudentSubject IDs are created in the domain model, rather than
            // by the database. Tell EF that these values are client-generated
            // so newly-added subjects are inserted instead of updated.
            subjectBuilder.Property(subject => subject.Id)
                .ValueGeneratedNever();

            subjectBuilder.Property(subject => subject.Type)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            subjectBuilder.Property(subject => subject.CustomName)
                .HasMaxLength(200);
        });
    }
}
