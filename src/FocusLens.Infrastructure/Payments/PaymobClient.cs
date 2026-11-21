using System.Buffers;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FocusLens.Domain.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace FocusLens.Infrastructure.Payments;

internal sealed class PaymobClient(HttpClient httpClient, IOptions<PaymobOptions> options)
{
    private static readonly string[] SignedFields =
    [
        "amount_cents", "created_at", "currency", "error_occured", "has_parent_transaction",
        "id", "integration_id", "is_3d_secure", "is_auth", "is_capture", "is_refunded",
        "is_standalone_payment", "is_voided", "order.id", "owner", "pending",
        "source_data.pan", "source_data.sub_type", "source_data.type", "success"
    ];

    public async Task<(long OrderId, string CheckoutUrl)> CreateIntentionAsync(
        PaymentPurchase purchase, ApplicationUser buyer, string phone, CancellationToken cancellationToken)
    {
        PaymobAccountOptions settings = options.Value.ForMode(purchase.IsSandbox);
        string name = $"FocusLens {purchase.Plan} ({purchase.Term})";
        using HttpRequestMessage request = new(HttpMethod.Post, "v1/intention/")
        {
            Content = JsonContent.Create(new
            {
                amount = purchase.AmountPiasters,
                currency = PaymentPurchase.Currency,
                payment_methods = new[] { purchase.IntegrationId },
                items = new[] { new { name, amount = purchase.AmountPiasters, quantity = 1 } },
                billing_data = new
                {
                    first_name = buyer.FirstName,
                    last_name = buyer.LastName,
                    email = buyer.Email,
                    phone_number = phone,
                    country = "EGY",
                    apartment = "NA",
                    floor = "NA",
                    street = "NA",
                    building = "NA",
                    shipping_method = "NA",
                    postal_code = "NA",
                    city = "NA",
                    state = "NA"
                },
                special_reference = $"{(purchase.IsSandbox ? "test" : "live")}-{purchase.BuyerId:N}-{purchase.Id:N}",
                expiration = (int)PaymentPurchase.CheckoutLifetime.TotalSeconds,
                notification_url = settings.NotificationUrl,
                redirection_url = QueryHelpers.AddQueryString(settings.ReturnUrl, "checkoutId", purchase.Id.ToString())
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Token", settings.SecretKey);

        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using JsonDocument body = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken)
            ?? throw new JsonException("Paymob returned an empty intention.");
        JsonElement intention = body.RootElement;
        if (intention.ValueKind != JsonValueKind.Object ||
            !intention.TryGetProperty("intention_order_id", out JsonElement order) ||
            order.ValueKind != JsonValueKind.Number || !order.TryGetInt64(out long orderId) || orderId <= 0 ||
            !intention.TryGetProperty("client_secret", out JsonElement secret) ||
            secret.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(secret.GetString()))
        {
            throw new JsonException("Paymob returned an incomplete intention.");
        }

        string url = QueryHelpers.AddQueryString(new Uri(httpClient.BaseAddress!, "unifiedcheckout/").AbsoluteUri,
            new Dictionary<string, string?> { ["publicKey"] = settings.PublicKey, ["clientSecret"] = secret.GetString() });
        return (orderId, url);
    }

    public static bool VerifySignature(JsonElement transaction, string? signature, string secret)
    {
        Span<byte> received = stackalloc byte[64];
        if (signature?.Length != 128 ||
            Convert.FromHexString(signature.AsSpan(), received, out _, out _) != OperationStatus.Done)
        {
            return false;
        }

        StringBuilder signed = new();
        foreach (string path in SignedFields)
        {
            JsonElement value = transaction;
            foreach (string segment in path.Split('.'))
            {
                if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
                {
                    return false;
                }
            }

            if (value.ValueKind is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Undefined)
            {
                return false;
            }

            signed.Append(value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Null => "",
                _ => value.GetRawText()
            });
        }

        byte[] expected = HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signed.ToString()));
        return CryptographicOperations.FixedTimeEquals(expected, received);
    }
}
