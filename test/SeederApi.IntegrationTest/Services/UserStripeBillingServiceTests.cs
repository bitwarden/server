using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Services;
using NSubstitute;
using Stripe;
using Xunit;
using PremiumPlan = Bit.Core.Billing.Pricing.Premium.Plan;
using PremiumPurchasable = Bit.Core.Billing.Pricing.Premium.Purchasable;

namespace Bit.SeederApi.IntegrationTest.Services;

/// <summary>
/// Covers the requests <see cref="UserStripeBillingService"/> sends to Stripe and the user fields it sets.
/// Nothing here reaches Stripe: <see cref="IStripeAdapter"/> and <see cref="IPricingClient"/> are substitutes.
/// </summary>
public class UserStripeBillingServiceTests
{
    private const string _seatPriceId = "premium-annually-2026";
    private const short _providedStorageGb = 5;
    private static readonly DateTime _periodEnd = new(2027, 10, 13, 0, 0, 0, DateTimeKind.Utc);

    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private CustomerCreateOptions? _sentCustomer;
    private SubscriptionCreateOptions? _sentSubscription;

    public UserStripeBillingServiceTests()
    {
        _pricingClient.GetAvailablePremiumPlan().Returns(new PremiumPlan
        {
            Name = "Premium",
            Available = true,
            Seat = new PremiumPurchasable { StripePriceId = _seatPriceId, Provided = 1 },
            Storage = new PremiumPurchasable { StripePriceId = "personal-storage-gb-annually", Provided = _providedStorageGb },
        });

        _stripeAdapter.CreateCustomerAsync(Arg.Do<CustomerCreateOptions>(o => _sentCustomer = o))
            .Returns(new Customer { Id = "cus_test" });

        _stripeAdapter.CreateSubscriptionAsync(Arg.Do<SubscriptionCreateOptions>(o => _sentSubscription = o))
            .Returns(new Subscription
            {
                Id = "sub_test",
                Items = new StripeList<SubscriptionItem>
                {
                    Data = [new SubscriptionItem { CurrentPeriodEnd = _periodEnd }],
                },
            });
    }

    [Fact]
    public async Task CreateSubscriptionAsync_AttachesTheStripeTestCardAsDefaultAsync()
    {
        var user = NewUser();

        await Build().CreateSubscriptionAsync(user, UserSubscriptionType.Premium);

        Assert.NotNull(_sentCustomer);
        Assert.Equal("pm_card_visa", _sentCustomer.PaymentMethod);
        Assert.Equal("pm_card_visa", _sentCustomer.InvoiceSettings.DefaultPaymentMethod);
        Assert.Equal("US", _sentCustomer.Address.Country);
        Assert.Equal("43432", _sentCustomer.Address.PostalCode);
        Assert.Equal("immediately", _sentCustomer.Tax.ValidateLocation);
        Assert.Equal(user.Email, _sentCustomer.Email);
    }

    [Fact]
    public async Task CreateSubscriptionAsync_TagsTheUserIdForBillingWebhooksAsync()
    {
        var user = NewUser();

        await Build().CreateSubscriptionAsync(user, UserSubscriptionType.Premium);

        Assert.NotNull(_sentSubscription);
        Assert.Equal(user.Id.ToString(), _sentSubscription.Metadata["userId"]);
    }

    [Fact]
    public async Task CreateSubscriptionAsync_BillsOneSeatOfThePremiumPlanAsync()
    {
        await Build().CreateSubscriptionAsync(NewUser(), UserSubscriptionType.Premium);

        Assert.NotNull(_sentSubscription);
        var item = Assert.Single(_sentSubscription.Items);
        Assert.Equal(_seatPriceId, item.Price);
        Assert.Equal(1, item.Quantity);
        Assert.True(_sentSubscription.AutomaticTax.Enabled);
    }

    [Fact]
    public async Task CreateSubscriptionAsync_StampsPremiumAndGatewayFieldsFromStripeAsync()
    {
        var user = NewUser();

        await Build().CreateSubscriptionAsync(user, UserSubscriptionType.Premium);

        Assert.True(user.Premium);
        Assert.Equal(_periodEnd, user.PremiumExpirationDate);
        Assert.Equal(GatewayType.Stripe, user.Gateway);
        Assert.Equal("cus_test", user.GatewayCustomerId);
        Assert.Equal("sub_test", user.GatewaySubscriptionId);
        Assert.Equal(_providedStorageGb, user.MaxStorageGb);
        Assert.Equal(20, user.LicenseKey?.Length);
    }

    [Fact]
    public async Task CreateSubscriptionAsync_SubscriptionFails_NamesTheOrphanedCustomerAndLeavesTheUserUnbilledAsync()
    {
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>())
            .Returns(Task.FromException<Subscription>(new StripeException("The card was declined.")));
        var user = NewUser();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Build().CreateSubscriptionAsync(user, UserSubscriptionType.Premium));

        Assert.Contains("cus_test", ex.Message);
        Assert.Contains(user.Id.ToString(), ex.Message);
        Assert.Null(user.GatewayCustomerId);
        Assert.Null(user.GatewaySubscriptionId);
    }

    [Fact]
    public async Task CreateSubscriptionAsync_CustomerCreationFails_ReportsNoCustomerAndLeavesTheUserUnbilledAsync()
    {
        _stripeAdapter.CreateCustomerAsync(Arg.Any<CustomerCreateOptions>())
            .Returns(Task.FromException<Customer>(new StripeException("Expired API Key provided.")));
        var user = NewUser();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Build().CreateSubscriptionAsync(user, UserSubscriptionType.Premium));

        Assert.Contains("Gateway customer: <none>", ex.Message);
        Assert.Contains(user.Id.ToString(), ex.Message);
        Assert.Null(user.GatewayCustomerId);
        Assert.Null(user.GatewaySubscriptionId);
    }

    [Fact]
    public async Task CreateSubscriptionAsync_NonTestKey_ThrowsBeforeCallingStripeAsync()
    {
        var service = Build(Settings(apiKey: "sk_live_abc123"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateSubscriptionAsync(NewUser(), UserSubscriptionType.Premium));

        Assert.Empty(_stripeAdapter.ReceivedCalls());
    }

    [Fact]
    public async Task CreateSubscriptionAsync_UnmappedSubscriptionType_ThrowsBeforeCallingStripeAsync()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => Build().CreateSubscriptionAsync(NewUser(), (UserSubscriptionType)999));

        Assert.Empty(_stripeAdapter.ReceivedCalls());
    }

    private UserStripeBillingService Build(GlobalSettings? globalSettings = null) =>
        new(globalSettings ?? Settings(), _pricingClient, _stripeAdapter);

    private static GlobalSettings Settings(string apiKey = "sk_test_abc123")
    {
        var settings = new GlobalSettings { SelfHosted = false, PricingUri = "https://billingpricing.qa.bitwarden.pw" };
        settings.Stripe.ApiKey = apiKey;
        return settings;
    }

    private static User NewUser() => new()
    {
        Id = Guid.NewGuid(),
        Email = "billed@individual.example",
        Name = "Billed User",
    };
}
