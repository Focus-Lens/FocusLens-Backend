using FocusLens.Infrastructure.Identity.Verification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public sealed class EmailVerificationCodeConfiguration
    : IEntityTypeConfiguration<EmailVerificationCode>
{
    public void Configure(EntityTypeBuilder<EmailVerificationCode> builder)
    {
        builder.ToTable("EmailVerificationCodes");

        builder.HasKey(code => code.Id);

        builder.Property(code => code.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(code => code.CodeHash)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(code => new { code.Email, code.Purpose, code.UsedOnUtc });
    }
}
