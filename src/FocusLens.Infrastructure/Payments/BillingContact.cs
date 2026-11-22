using FocusLens.Contracts.Payments;

namespace FocusLens.Infrastructure.Payments;

internal sealed class BillingContact
{
    public Guid BuyerId { get; init; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Address { get; set; }

    public BillingContactResponse ToResponse() => new(FullName, Email, City, Country, Address);
}
