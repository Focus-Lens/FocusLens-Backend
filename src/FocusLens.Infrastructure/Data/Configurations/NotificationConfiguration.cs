using FocusLens.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.HasKey(notification => notification.Id);

        builder.Property(notification => notification.RecipientUserId).IsRequired();

        builder.HasOne(notification => notification.RecipientUser)
            .WithMany()
            .HasForeignKey(notification => notification.RecipientUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(notification => notification.Audience)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(notification => notification.Category)
            .HasConversion<string>()
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(notification => notification.Title)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(notification => notification.Message)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(notification => notification.ActionUrl)
            .HasMaxLength(500);

        builder.Property(notification => notification.ActionText)
            .HasMaxLength(80);

        builder.Property(notification => notification.DedupeKey)
            .HasMaxLength(300);

        builder.Property(notification => notification.PushFailureReason)
            .HasMaxLength(300);

        builder.HasIndex(notification => notification.RecipientUserId);
        builder.HasIndex(notification => new { notification.RecipientUserId, notification.ReadAtUtc });
        builder.HasIndex(notification => new { notification.RecipientUserId, notification.PushSentAtUtc });
        builder.HasIndex(notification => notification.DedupeKey)
            .IsUnique()
            .HasFilter("[DedupeKey] IS NOT NULL");
    }
}