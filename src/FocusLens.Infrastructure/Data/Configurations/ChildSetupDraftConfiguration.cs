using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public sealed class ChildSetupDraftConfiguration
    : IEntityTypeConfiguration<ChildSetupDraft>
{
    public void Configure(EntityTypeBuilder<ChildSetupDraft> builder)
    {
        builder.ToTable("ChildSetupDrafts");

        builder.HasKey(draft => draft.Id);

        builder.Property(draft => draft.ParentId)
            .IsRequired();

        builder.HasIndex(draft => draft.ParentId);

        builder.HasOne<Parent>()
            .WithMany()
            .HasForeignKey(draft => draft.ParentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Student>()
            .WithMany()
            .HasForeignKey(draft => draft.ClaimedByStudentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(draft => draft.ClaimedByStudentId);

        builder.Property(draft => draft.FirstName)
            .HasMaxLength(100);

        builder.Property(draft => draft.LastName)
            .HasMaxLength(100);

        builder.Property(draft => draft.DateOfBirth);

        builder.Property(draft => draft.Grade)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(draft => draft.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.OwnsMany(draft => draft.Subjects, subjectBuilder =>
        {
            subjectBuilder.ToTable("ChildSetupSubjects");

            subjectBuilder.HasKey(subject => subject.Id);

            subjectBuilder.Property(subject => subject.Id)
                .ValueGeneratedNever();

            subjectBuilder.Property(subject => subject.Type)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            subjectBuilder.Property(subject => subject.CustomName)
                .HasMaxLength(200);
        });


        builder.PrimitiveCollection(draft => draft.StudyPriorities)
            .HasColumnName("StudyPriorities")
            .ElementType(elementBuilder =>
                elementBuilder.HasConversion<string>());

        builder.OwnsOne(draft => draft.StudyTimeGoal, goalBuilder =>
        {
            goalBuilder.Property(goal => goal.Period)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasColumnName("StudyTimeGoalPeriod");

            goalBuilder.Property(goal => goal.TargetMinutes)
                .HasColumnName("StudyTimeGoalTargetMinutes");

            goalBuilder.Property(goal => goal.Days)
                .HasConversion(
                    StudyTimeGoalDaysConverter.Converter,
                    StudyTimeGoalDaysConverter.Comparer)
                .HasColumnName("StudyTimeGoalDays");

            goalBuilder.Property(goal => goal.StartDate)
                .HasColumnName("StudyTimeGoalStartDate");
        });
    }
}