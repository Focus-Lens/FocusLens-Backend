namespace FocusLens.Infrastructure.Payments;

internal sealed class PaymobOptions
{
    public bool EnableLiveCheckout { get; init; }
    public PaymobAccountOptions Sandbox { get; init; } = new();
    public PaymobAccountOptions Live { get; init; } = new();

    public PaymobAccountOptions ForMode(bool isSandbox) => isSandbox ? Sandbox : Live;
}

internal sealed class PaymobAccountOptions
{
    public string SecretKey { get; init; } = "";
    public string PublicKey { get; init; } = "";
    public string HmacSecret { get; init; } = "";
    public int CardIntegrationId { get; init; }
    public int WalletIntegrationId { get; init; }
    public string NotificationUrl { get; init; } = "";
    public string ReturnUrl { get; init; } = "";

    public bool IsConfigured(bool isSandbox)
    {
        string expectedMode = isSandbox ? "test" : "live";
        return HasKeyPrefix(SecretKey, "sk", expectedMode) &&
               HasKeyPrefix(PublicKey, "pk", expectedMode) &&
               !string.IsNullOrWhiteSpace(HmacSecret) &&
               IsHttps(ReturnUrl) &&
               IsHttps(NotificationUrl) &&
               new Uri(NotificationUrl).AbsolutePath.EndsWith(
                   "/api/payments/paymob/webhook/" + (isSandbox ? "sandbox" : "live"), StringComparison.Ordinal);
    }

    private static bool HasKeyPrefix(string key, string type, string mode)
    {
        string value = key.StartsWith("egy_", StringComparison.Ordinal) ? key[4..] : key;
        return value.StartsWith($"{type}_{mode}_", StringComparison.Ordinal) ||
               value.StartsWith($"{type}{mode}_", StringComparison.Ordinal);
    }

    private static bool IsHttps(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps;
}
