using FocusLens.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public sealed class UserDeviceTokenConfiguration : IEntityTypeConfiguration<UserDeviceToken>
{
    public void Configure(EntityTypeBuilder<UserDeviceToken> builder)
    {
        builder.ToTable("UserDeviceTokens");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.UserId).IsRequired();

        builder.HasOne(token => token.User)
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(token => token.Platform)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(token => token.Token)
            .HasMaxLength(2048)
            .IsRequired();

        builder.HasIndex(token => token.UserId);
        builder.HasIndex(token => token.Token).IsUnique();
    }
}