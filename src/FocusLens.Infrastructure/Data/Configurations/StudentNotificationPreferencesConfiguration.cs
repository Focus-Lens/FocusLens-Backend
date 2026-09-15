using FocusLens.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public sealed class StudentNotificationPreferencesConfiguration
    : IEntityTypeConfiguration<StudentNotificationPreferences>
{
    public void Configure(EntityTypeBuilder<StudentNotificationPreferences> builder)
    {
        builder.ToTable("StudentNotificationPreferences");

        builder.HasKey(preferences => preferences.Id);

        builder.Property(preferences => preferences.StudentUserId).IsRequired();

        builder.HasOne(preferences => preferences.StudentUser)
            .WithOne()
            .HasForeignKey<StudentNotificationPreferences>(preferences => preferences.StudentUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(preferences => preferences.StudyRemindersEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(preferences => preferences.SessionSummariesEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(preferences => preferences.ParentActivityEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(preferences => preferences.ReminderTime)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(preferences => preferences.StudentUserId).IsUnique();
    }
}
