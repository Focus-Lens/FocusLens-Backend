using FocusLens.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public sealed class UserTermsAcceptanceConfiguration
    : IEntityTypeConfiguration<UserTermsAcceptance>
{
    public void Configure(EntityTypeBuilder<UserTermsAcceptance> builder)
    {
        builder.HasIndex(acceptance => new
        {
            acceptance.UserId,
            acceptance.LegalDocumentId
        })
            .IsUnique();

        builder.HasOne(acceptance => acceptance.User)
            .WithMany(user => user.TermsAcceptances)
            .HasForeignKey(acceptance => acceptance.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(acceptance => acceptance.LegalDocument)
            .WithMany()
            .HasForeignKey(acceptance => acceptance.LegalDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
