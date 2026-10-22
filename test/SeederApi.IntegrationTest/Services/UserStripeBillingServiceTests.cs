using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Options;
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
    private static readonly StripeBillingProfile _defaultProfile = new();

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
    public async Task CreateSubscriptionAsync_DefaultProfile_AttachesTheVisaTestCardAtTheUsAddressAsync()
    {
        var user = NewUser();

        await Build().CreateSubscriptionAsync(user, UserSubscriptionType.Premium, _defaultProfile);

        Assert.NotNull(_sentCustomer);
        Assert.Equal("pm_card_visa", _sentCustomer.PaymentMethod);
        Assert.Equal("pm_card_visa", _sentCustomer.InvoiceSettings.DefaultPaymentMethod);
        Assert.Equal("US", _sentCustomer.Address.Country);
        Assert.Equal("43432", _sentCustomer.Address.PostalCode);
        Assert.Null(_sentCustomer.TaxIdData);
        Assert.Equal("immediately", _sentCustomer.Tax.ValidateLocation);
        Assert.Equal(user.Email, _sentCustomer.Email);
    }

    [Fact]
    public async Task CreateSubscriptionAsync_NonDefaultProfile_SendsItsCardAndAddressAsync()
    {
        var profile = new StripeBillingProfile { Card = StripeTestCard.Mastercard, Address = StripeTestAddress.Germany };

        await Build().CreateSubscriptionAsync(NewUser(), UserSubscriptionType.Premium, profile);

        Assert.NotNull(_sentCustomer);
        Assert.Equal("pm_card_mastercard", _sentCustomer.PaymentMethod);
        Assert.Equal("pm_card_mastercard", _sentCustomer.InvoiceSettings.DefaultPaymentMethod);
        Assert.Equal("DE", _sentCustomer.Address.Country);
        Assert.Equal("10117", _sentCustomer.Address.PostalCode);
    }

    [Fact]
    public async Task CreateSubscriptionAsync_TaxIdAddress_SendsNoTaxIdAsync()
    {
        // Production premium signup never sends a tax ID, so the seeder doesn't either.
        var profile = new StripeBillingProfile { Address = StripeTestAddress.GermanyWithVatId };

        await Build().CreateSubscriptionAsync(NewUser(), UserSubscriptionType.Premium, profile);

        Assert.NotNull(_sentCustomer);
        Assert.Null(_sentCustomer.TaxIdData);
    }

    [Fact]
    public async Task CreateSubscriptionAsync_IncompleteSubscription_StillStampsPremiumAndExpiryAsync()
    {
        // The fails-on-charge card leaves the subscription incomplete; production still grants premium.
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>())
            .Returns(new Subscription
            {
                Id = "sub_incomplete",
                Status = "incomplete",
                Items = new StripeList<SubscriptionItem>
                {
                    Data = [new SubscriptionItem { CurrentPeriodEnd = _periodEnd }],
                },
            });
        var user = NewUser();
        var profile = new StripeBillingProfile { Card = StripeTestCard.FailsOnCharge };

        await Build().CreateSubscriptionAsync(user, UserSubscriptionType.Premium, profile);

        Assert.True(user.Premium);
        Assert.Equal(_periodEnd, user.PremiumExpirationDate);
        Assert.Equal("sub_incomplete", user.GatewaySubscriptionId);
    }

    [Fact]
    public async Task CreateSubscriptionAsync_TagsTheUserIdForBillingWebhooksAsync()
    {
        var user = NewUser();

        await Build().CreateSubscriptionAsync(user, UserSubscriptionType.Premium, _defaultProfile);

        Assert.NotNull(_sentSubscription);
        Assert.Equal(user.Id.ToString(), _sentSubscription.Metadata["userId"]);
    }

    [Fact]
    public async Task CreateSubscriptionAsync_BillsOneSeatOfThePremiumPlanAsync()
    {
        await Build().CreateSubscriptionAsync(NewUser(), UserSubscriptionType.Premium, _defaultProfile);

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

        await Build().CreateSubscriptionAsync(user, UserSubscriptionType.Premium, _defaultProfile);

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
            () => Build().CreateSubscriptionAsync(user, UserSubscriptionType.Premium, _defaultProfile));

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
            () => Build().CreateSubscriptionAsync(user, UserSubscriptionType.Premium, _defaultProfile));

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
            () => service.CreateSubscriptionAsync(NewUser(), UserSubscriptionType.Premium, _defaultProfile));

        Assert.Empty(_stripeAdapter.ReceivedCalls());
    }

    [Fact]
    public async Task CreateSubscriptionAsync_UnmappedSubscriptionType_ThrowsBeforeCallingStripeAsync()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => Build().CreateSubscriptionAsync(NewUser(), (UserSubscriptionType)999, _defaultProfile));

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
