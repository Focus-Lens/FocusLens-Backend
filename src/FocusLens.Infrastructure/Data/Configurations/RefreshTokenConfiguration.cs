using FocusLens.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(refreshToken => refreshToken.Id);

        builder.Property(refreshToken => refreshToken.Token)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(refreshToken => refreshToken.Token)
            .IsUnique();

        builder.Property(refreshToken => refreshToken.UserId)
            .IsRequired();

        builder.Property(refreshToken => refreshToken.ExpiresOnUtc)
            .IsRequired();

        builder.Property(refreshToken => refreshToken.RevokedOnUtc)
            .IsRequired(false);
    }
}
