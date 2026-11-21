using FocusLens.Domain.Identity;
using FocusLens.Infrastructure.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

internal sealed class PaymentPurchaseConfiguration : IEntityTypeConfiguration<PaymentPurchase>
{
    public void Configure(EntityTypeBuilder<PaymentPurchase> builder)
    {
        builder.ToTable("PaymentPurchases");
        builder.HasKey(purchase => new { purchase.BuyerId, purchase.Id, purchase.IsSandbox });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(purchase => purchase.BuyerId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(purchase => new { purchase.IsSandbox, purchase.ProviderOrderId }).IsUnique();
        builder.HasIndex(purchase => new { purchase.IsSandbox, purchase.PaidTransactionId }).IsUnique();
        builder.Property(purchase => purchase.Status).IsConcurrencyToken();
        builder.Property(purchase => purchase.CheckoutUrl).HasMaxLength(2048);
    }
}
