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
        builder.HasIndex(purchase => new { purchase.BuyerId, purchase.IsSandbox, purchase.Status, purchase.PaidAtUtc });
        builder.HasIndex(purchase => new { purchase.CancellationApplied, purchase.PaidThroughUtc })
            .HasFilter("[CancellationRequestedAtUtc] IS NOT NULL");
        builder.Property(purchase => purchase.CancellationRequestedAtUtc).IsConcurrencyToken();
        builder.Property(purchase => purchase.KeepStudentId).IsConcurrencyToken();
        builder.Property(purchase => purchase.CancellationApplied).IsConcurrencyToken();
        builder.Property(purchase => purchase.CardBrand).HasMaxLength(20);
        builder.Property(purchase => purchase.CardLastFour).HasMaxLength(4);
        builder.Property(purchase => purchase.CheckoutUrl).HasMaxLength(2048);
    }
}
