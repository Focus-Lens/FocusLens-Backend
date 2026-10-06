using FocusLens.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public sealed class LegalDocumentConfiguration : IEntityTypeConfiguration<LegalDocument>
{
    public void Configure(EntityTypeBuilder<LegalDocument> builder)
    {
        builder.Property(document => document.Audience)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(document => document.Type)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(document => document.Version)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(document => document.Content)
            .IsRequired();

        builder.HasIndex(document => new { document.Audience, document.Type, document.Version })
            .IsUnique();

        builder.HasIndex(document => new { document.Audience, document.Type, document.IsPublished, document.PublishedOnUtc });
    }
}
