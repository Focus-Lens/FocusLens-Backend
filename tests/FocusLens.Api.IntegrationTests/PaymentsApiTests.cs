using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FocusLens.API;
using FocusLens.Contracts.Payments;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Identity;
using FocusLens.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FocusLens.Api.IntegrationTests;

public sealed class PaymentsApiTests
{
    private const string Checkouts = "/api/payments/checkouts";
    private const string Callback = "/api/payments/paymob/webhook";
    private const string HmacSecret = "test-signing-secret";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task StudentPlus_IsBuyerScopedAndRecordsPaidTermOnceAfterVerifiedCallback()
    {
        DateTimeOffset now = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);
        await using CustomWebApplicationFactory root = new(now);
        PaymobStub paymob = new();
        await using WebApplicationFactory<Program> factory = Configure(root, paymob);
        ApplicationUser buyer = NewUser();
        ApplicationUser otherUser = NewUser();
        Student student = new(buyer.Id);
        await SeedAsync(factory, db =>
        {
            db.Users.AddRange(buyer, otherUser);
            db.Students.AddRange(student, new Student(otherUser.Id));
        });

        using HttpClient client = CreateClient(factory, buyer.Id, "Student");
        Guid id = Guid.NewGuid();
        StartCheckoutRequest request = new(id, PaidPlan.Plus, BillingTerm.Monthly, PaymentMethod.Card, null, null);
        using HttpResponseMessage response = await client.PostAsJsonAsync(Checkouts, request, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        CheckoutResponse checkout = (await response.Content.ReadFromJsonAsync<CheckoutResponse>(JsonOptions))!;
        Assert.Equal([student.Id], checkout.StudentIds);
        Assert.Equal(14900, checkout.AmountPiasters);
        Assert.Equal(PaymentStatus.Pending, checkout.Status);
        Assert.True(checkout.IsSandbox);
        Assert.StartsWith("https://accept.paymob.com/unifiedcheckout/", checkout.CheckoutUrl);
        Assert.Equal(101, paymob.Body.GetProperty("payment_methods")[0].GetInt32());
        Assert.Equal(14900, paymob.Body.GetProperty("amount").GetInt32());
        Assert.Equal("EGP", paymob.Body.GetProperty("currency").GetString());
        Assert.EndsWith($"?checkoutId={id}", paymob.Body.GetProperty("redirection_url").GetString());

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Checkouts, request, JsonOptions)).StatusCode);
        Assert.Equal(1, paymob.CallCount);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Checkouts,
            request with { Term = BillingTerm.Yearly }, JsonOptions)).StatusCode);

        using HttpClient other = CreateClient(factory, otherUser.Id, "Student");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{Checkouts}/{id}")).StatusCode);

        using HttpClient anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await SendCallback(anonymous, 14900, paid: true, signature: new string('0', 128))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendCallback(anonymous, 29900, paid: true)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendCallback(anonymous, 14900, paid: false, pending: true)).StatusCode);
        Assert.Equal(PaymentStatus.Pending, (await GetCheckout(client, id)).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await SendCallback(anonymous, 14900, paid: false)).StatusCode);
        Assert.Equal(PaymentStatus.Failed, (await GetCheckout(client, id)).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await SendCallback(anonymous, 14900, paid: true)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendCallback(anonymous, 14900, paid: true)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendCallback(anonymous, 14900, paid: false)).StatusCode);

        CheckoutResponse paid = await GetCheckout(client, id);
        Assert.Equal(PaymentStatus.Paid, paid.Status);
        Assert.Equal(now.AddMonths(1), paid.PaidThroughUtc);
        Assert.Null(paid.CheckoutUrl);
    }

    [Fact]
    public async Task Checkout_EnforcesStudentSelfPurchaseAndParentLinks()
    {
        await using CustomWebApplicationFactory root = new();
        PaymobStub paymob = new();
        await using WebApplicationFactory<Program> factory = Configure(root, paymob);
        ApplicationUser parentUser = NewUser();
        ApplicationUser childUser = NewUser();
        ApplicationUser otherUser = NewUser();
        Parent parent = new(parentUser.Id);
        Student child = new(childUser.Id);
        Student other = new(otherUser.Id);
        ParentStudentRelationship link = new(parent.Id, child.Id);
        link.Accept();
        await SeedAsync(factory, db =>
        {
            db.Users.AddRange(parentUser, childUser, otherUser);
            db.Parents.Add(parent);
            db.Students.AddRange(child, other);
            db.ParentStudentRelationships.Add(link);
        });

        using HttpClient studentClient = CreateClient(factory, childUser.Id, "Student");
        using HttpClient parentClient = CreateClient(factory, parentUser.Id, "Parent");
        StartCheckoutRequest request = new(Guid.NewGuid(), PaidPlan.Family, BillingTerm.Yearly,
            PaymentMethod.MobileWallet, [child.Id], null);

        Assert.Equal(HttpStatusCode.Forbidden, (await studentClient.PostAsJsonAsync(Checkouts, request, JsonOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await studentClient.PostAsJsonAsync(Checkouts,
            request with { Plan = PaidPlan.Plus, StudentIds = [other.Id] }, JsonOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await parentClient.PostAsJsonAsync(Checkouts,
            request with { StudentIds = [child.Id, other.Id] }, JsonOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await parentClient.PostAsJsonAsync(Checkouts,
            request with { Plan = PaidPlan.Plus, StudentIds = [child.Id, other.Id] }, JsonOptions)).StatusCode);
        Assert.Equal(0, paymob.CallCount);

        using HttpResponseMessage family = await parentClient.PostAsJsonAsync(Checkouts, request, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, family.StatusCode);
        CheckoutResponse checkout = (await family.Content.ReadFromJsonAsync<CheckoutResponse>(JsonOptions))!;
        Assert.Equal([child.Id], checkout.StudentIds);
        Assert.Equal(287040, checkout.AmountPiasters);
        Assert.Equal(102, paymob.Body.GetProperty("payment_methods")[0].GetInt32());

        using HttpClient anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NoContent, (await SendCallback(anonymous, 287040, paid: true, wallet: true)).StatusCode);
        Assert.Equal(PaymentStatus.Paid, (await GetCheckout(parentClient, request.CheckoutId)).Status);

        StartCheckoutRequest parentPlus = request with
        {
            CheckoutId = Guid.NewGuid(),
            Plan = PaidPlan.Plus,
            Term = BillingTerm.Monthly,
            Method = PaymentMethod.Card
        };
        using HttpResponseMessage plus = await parentClient.PostAsJsonAsync(Checkouts, parentPlus, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, plus.StatusCode);
        CheckoutResponse plusCheckout = (await plus.Content.ReadFromJsonAsync<CheckoutResponse>(JsonOptions))!;
        Assert.Equal([child.Id], plusCheckout.StudentIds);
        Assert.Equal(14900, plusCheckout.AmountPiasters);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LiveCheckout_RequiresLiveKeysAndConfirmsOnlyWithLiveCallback(bool useTestKeysForLive)
    {
        await using CustomWebApplicationFactory root = new();
        PaymobStub paymob = new(live: true);
        await using WebApplicationFactory<Program> factory =
            Configure(root, paymob, enableLive: true, useTestKeysForLive: useTestKeysForLive);
        ApplicationUser buyer = NewUser();
        await SeedAsync(factory, db =>
        {
            db.Users.Add(buyer);
            db.Students.Add(new Student(buyer.Id));
        });
        using HttpClient client = CreateClient(factory, buyer.Id, "Student");
        Guid id = Guid.NewGuid();
        StartCheckoutRequest request = new(id, PaidPlan.Plus, BillingTerm.Monthly, PaymentMethod.Card, null, null);
        using HttpResponseMessage response = await client.PostAsJsonAsync(Checkouts, request, JsonOptions);
        if (useTestKeysForLive)
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(0, paymob.CallCount);
            return;
        }

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        CheckoutResponse checkout = (await response.Content.ReadFromJsonAsync<CheckoutResponse>(JsonOptions))!;
        Assert.False(checkout.IsSandbox);
        Assert.Equal(201, paymob.Body.GetProperty("payment_methods")[0].GetInt32());
        Assert.EndsWith("/live", paymob.Body.GetProperty("notification_url").GetString());
        Assert.Contains("publicKey=egy_pk_live_example", checkout.CheckoutUrl);

        using HttpClient anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NoContent, (await SendCallback(anonymous, 14900, paid: true)).StatusCode);
        Assert.Equal(PaymentStatus.Pending, (await GetCheckout(client, id)).Status);

        Assert.Equal(HttpStatusCode.NoContent, (await SendCallback(anonymous, 14900, paid: true, live: true)).StatusCode);
        CheckoutResponse paid = await GetCheckout(client, id);
        Assert.Equal(PaymentStatus.Paid, paid.Status);
        Assert.NotNull(paid.PaidThroughUtc);
    }

    [Fact]
    public async Task ParentBilling_UsesVerifiedPaymentAndSupportsScheduledCancellationAndResume()
    {
        DateTimeOffset now = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);
        TestClock clock = new(now);
        await using CustomWebApplicationFactory root = new(now);
        PaymobStub paymob = new();
        await using WebApplicationFactory<Program> factory = Configure(root, paymob, clock: clock);
        ApplicationUser buyer = NewUser();
        ApplicationUser firstUser = NewUser();
        ApplicationUser secondUser = NewUser();
        Parent parent = new(buyer.Id);
        Student first = new(firstUser.Id);
        Student second = new(secondUser.Id);
        ParentStudentRelationship firstLink = new(parent.Id, first.Id);
        ParentStudentRelationship secondLink = new(parent.Id, second.Id);
        firstLink.Accept(now);
        secondLink.Accept(now);
        await SeedAsync(factory, db =>
        {
            db.Users.AddRange(buyer, firstUser, secondUser);
            db.Parents.Add(parent);
            db.Students.AddRange(first, second);
            db.ParentStudentRelationships.AddRange(firstLink, secondLink);
        });

        const string billingUrl = "/api/payments/billing";
        using HttpClient client = CreateClient(factory, buyer.Id, "Parent");
        using HttpClient studentClient = CreateClient(factory, firstUser.Id, "Student");
        Assert.Equal(HttpStatusCode.Forbidden, (await studentClient.GetAsync(billingUrl)).StatusCode);
        Assert.Equal(BillingStatus.Free, (await ReadBilling()).Status);
        UpdateBillingContactRequest contact = new("Billing Parent", "billing@example.test", "Cairo", "EG", null);
        using HttpResponseMessage saved = await client.PutAsJsonAsync(billingUrl + "/contact", contact, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        Guid id = Guid.NewGuid();
        StartCheckoutRequest request = new(id, PaidPlan.Family, BillingTerm.Monthly, PaymentMethod.Card,
            [first.Id, second.Id], null);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Checkouts, request, JsonOptions)).StatusCode);
        Assert.Equal("billing@example.test", paymob.Body.GetProperty("billing_data").GetProperty("email").GetString());

        BillingOverview pending = await ReadBilling();
        Assert.Null(pending.LastPayment);
        Assert.Equal(PaymentStatus.Pending, pending.OpenCheckout?.Status);
        using HttpClient webhook = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NoContent, (await SendCallback(webhook, 29900, paid: false)).StatusCode);
        Assert.Equal(BillingStatus.PaymentFailed, (await ReadBilling()).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await SendCallback(webhook, 29900, paid: true)).StatusCode);

        BillingOverview active = await ReadBilling();
        Assert.Equal(BillingStatus.Active, active.Status);
        Assert.Equal("2346", active.LastPayment?.CardLastFour);
        Assert.Equal("Mastercard", active.LastPayment?.CardBrand);
        Assert.Equal(now.AddMonths(1), active.LastPayment?.PaidThroughUtc);
        Assert.Equal("billing@example.test", active.Contact.Email);
        Assert.Equal(2, active.BeneficiaryIds.Length);

        string cancelUrl = billingUrl + "/cancellation";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(cancelUrl,
            new ScheduleCancellationRequest(id, Guid.NewGuid()), JsonOptions)).StatusCode);
        ScheduleCancellationRequest cancellation = new(id, first.Id);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(cancelUrl, cancellation, JsonOptions)).StatusCode);
        BillingOverview scheduled = await ReadBilling();
        Assert.Equal(BillingStatus.CancellationScheduled, scheduled.Status);
        Assert.Equal(first.Id, scheduled.KeepStudentId);
        Assert.Equal(now.AddMonths(1), scheduled.CancellationEffectiveAtUtc);
        Assert.All(scheduled.Children, child => Assert.False(child.IsPaused));

        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"{cancelUrl}/{id}")).StatusCode);
        Assert.Equal(BillingStatus.Active, (await ReadBilling()).Status);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(cancelUrl, cancellation, JsonOptions)).StatusCode);

        clock.Now = now.AddMonths(1).AddMinutes(1);
        BillingOverview ended = await ReadBilling();
        Assert.Equal(BillingStatus.Free, ended.Status);
        Assert.False(ended.Children.Single(child => child.StudentId == first.Id).IsPaused);
        Assert.True(ended.Children.Single(child => child.StudentId == second.Id).IsPaused);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"{cancelUrl}/{id}")).StatusCode);
        using JsonDocument connected = JsonDocument.Parse(await client.GetStringAsync("/api/access/students"));
        Assert.Equal(2, connected.RootElement.GetArrayLength());

        async Task<BillingOverview> ReadBilling() =>
            (await client.GetFromJsonAsync<BillingOverview>(billingUrl, JsonOptions))!;
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static WebApplicationFactory<Program> Configure(
        CustomWebApplicationFactory root, PaymobStub paymob, bool enableLive = false, bool useTestKeysForLive = false,
        TimeProvider? clock = null) =>
        root.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Paymob:Sandbox:SecretKey", "sk_test_example");
            builder.UseSetting("Paymob:Sandbox:PublicKey", "pk_test_example");
            builder.UseSetting("Paymob:Sandbox:HmacSecret", HmacSecret);
            builder.UseSetting("Paymob:Sandbox:CardIntegrationId", "101");
            builder.UseSetting("Paymob:Sandbox:WalletIntegrationId", "102");
            builder.UseSetting("Paymob:Sandbox:NotificationUrl", "https://api.focuslens.test" + Callback + "/sandbox");
            builder.UseSetting("Paymob:Sandbox:ReturnUrl", "https://focuslens.test/checkout");
            builder.UseSetting("Paymob:EnableLiveCheckout", enableLive.ToString());
            builder.UseSetting("Paymob:Live:SecretKey", useTestKeysForLive ? "sk_test_example" : "egy_sk_live_example");
            builder.UseSetting("Paymob:Live:PublicKey", useTestKeysForLive ? "pk_test_example" : "egy_pk_live_example");
            builder.UseSetting("Paymob:Live:HmacSecret", "live-signing-secret");
            builder.UseSetting("Paymob:Live:CardIntegrationId", "201");
            builder.UseSetting("Paymob:Live:NotificationUrl", "https://api.focuslens.test" + Callback + "/live");
            builder.UseSetting("Paymob:Live:ReturnUrl", "https://focuslens.test/live-checkout");
            builder.ConfigureServices(services =>
            {
                if (clock is not null)
                {
                    services.AddSingleton(clock);
                }

                services.AddHttpClient("PaymobClient").ConfigurePrimaryHttpMessageHandler(() => paymob);
            });
        });

    private static async Task SeedAsync(WebApplicationFactory<Program> factory,
        Action<ApplicationDbContext> seed)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();
        seed(db);
        await db.SaveChangesAsync();
    }

    private static ApplicationUser NewUser() => new()
    {
        Email = $"{Guid.NewGuid():N}@example.test",
        PhoneNumber = "+201000000000",
        FirstName = "Test",
        LastName = "Buyer"
    };

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, Guid userId, string role)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtTokenFactory.Create(userId, role));
        return client;
    }

    private static Task<HttpResponseMessage> SendCallback(HttpClient client, int amount, bool paid, bool pending = false,
        string? signature = null, bool wallet = false, bool live = false)
    {
        const string date = "2026-11-21T10:00:00.000000";
        string pendingText = pending ? "true" : "false";
        string successText = paid ? "true" : "false";
        int integration = live ? 201 : wallet ? 102 : 101;
        string? subtype = wallet ? null : "MasterCard";
        string source = wallet ? "wallet" : "card";
        string signed = $"{amount}{date}EGPfalsefalse7701{integration}truefalsefalsefalsetruefalse900122{pendingText}2346{subtype}{source}{successText}";
        signature ??= Convert.ToHexStringLower(HMACSHA512.HashData(
            Encoding.UTF8.GetBytes(live ? "live-signing-secret" : HmacSecret), Encoding.UTF8.GetBytes(signed)));

        return client.PostAsJsonAsync($"{Callback}/{(live ? "live" : "sandbox")}?hmac={signature}", new
        {
            type = "TRANSACTION",
            obj = new
            {
                amount_cents = amount, created_at = date, currency = "EGP", error_occured = false,
                has_parent_transaction = false, id = 7701, integration_id = integration, is_3d_secure = true,
                is_auth = false, is_capture = false, is_refunded = false, is_standalone_payment = true,
                is_voided = false, order = new { id = 9001 }, owner = 22, pending,
                source_data = new { pan = "2346", sub_type = subtype, type = source }, success = paid
            }
        });
    }

    private static async Task<CheckoutResponse> GetCheckout(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<CheckoutResponse>($"{Checkouts}/{id}", JsonOptions))!;

    private sealed class PaymobStub(bool live = false) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public JsonElement Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            Assert.Equal("https://accept.paymob.com/v1/intention/", request.RequestUri?.AbsoluteUri);
            Assert.Equal(live ? "Token egy_sk_live_example" : "Token sk_test_example", request.Headers.Authorization?.ToString());
            using JsonDocument body = await request.Content!.ReadFromJsonAsync<JsonDocument>(cancellationToken)
                ?? throw new InvalidOperationException("Intention body missing.");
            Body = body.RootElement.Clone();
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = JsonContent.Create(new { intention_order_id = 9000 + CallCount, client_secret = "egy_csk_test_example" })
            };
        }
    }
}
