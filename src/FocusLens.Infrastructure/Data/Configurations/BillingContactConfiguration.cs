using FocusLens.Domain.Identity;
using FocusLens.Infrastructure.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

internal sealed class BillingContactConfiguration : IEntityTypeConfiguration<BillingContact>
{
    public void Configure(EntityTypeBuilder<BillingContact> builder)
    {
        builder.ToTable("BillingContacts");
        builder.HasKey(contact => contact.BuyerId);
        builder.HasOne<ApplicationUser>().WithOne().HasForeignKey<BillingContact>(contact => contact.BuyerId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Property(contact => contact.FullName).HasMaxLength(150);
        builder.Property(contact => contact.Email).HasMaxLength(254);
        builder.Property(contact => contact.City).HasMaxLength(100);
        builder.Property(contact => contact.Country).HasMaxLength(2);
        builder.Property(contact => contact.Address).HasMaxLength(300);
    }
}
