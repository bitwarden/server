using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Premium.Commands;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Platform.Push;
using Bit.Core.Services;
using NSubstitute;
using Stripe;
using Xunit;
using PremiumPlan = Bit.Core.Billing.Pricing.Premium.Plan;
using PremiumPurchasable = Bit.Core.Billing.Pricing.Premium.Purchasable;

namespace Bit.Core.Test.Billing.Premium.Commands;

public class RestoreExistingPremiumSubscriptionCommandTests
{
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IPushNotificationService _pushNotificationService = Substitute.For<IPushNotificationService>();
    private readonly RestoreExistingPremiumSubscriptionCommand _command;

    public RestoreExistingPremiumSubscriptionCommandTests()
    {
        _pricingClient.GetAvailablePremiumPlan().Returns(new PremiumPlan
        {
            Seat = new PremiumPurchasable { StripePriceId = StripeConstants.Prices.PremiumAnnually },
            Storage = new PremiumPurchasable { StripePriceId = StripeConstants.Prices.StoragePlanPersonal, Provided = 1 }
        });
        _command = new RestoreExistingPremiumSubscriptionCommand(_stripeAdapter, _pricingClient, _userService, _pushNotificationService);
    }

    [Fact]
    public async Task Run_UserHasNoCustomer_ReturnsNullWithoutStripeCall()
    {
        var user = new User { Id = Guid.NewGuid() };

        var result = await _command.Run(user);

        Assert.Null(result);
        await _stripeAdapter.DidNotReceiveWithAnyArgs().ListSubscriptionsAsync(default);
    }

    [Theory]
    [InlineData(StripeConstants.SubscriptionStatus.Active)]
    [InlineData(StripeConstants.SubscriptionStatus.Trialing)]
    public async Task Run_ActiveSubscription_RestoresPremiumFromIt(string status)
    {
        var periodEnd = DateTime.UtcNow.AddMonths(11);
        var user = new User { Id = Guid.NewGuid(), Premium = false, GatewayCustomerId = "cus_1", GatewaySubscriptionId = "sub_ended" };
        CustomerSubscriptions(Subscription("sub_live", status, user.Id, periodEnd));

        var result = await _command.Run(user);

        Assert.Equal("sub_live", result?.Id);
        Assert.True(user.Premium);
        Assert.Equal("sub_live", user.GatewaySubscriptionId);
        Assert.Equal(periodEnd, user.PremiumExpirationDate);
        Assert.Equal(GatewayType.Stripe, user.Gateway);
        await _userService.Received(1).SaveUserAsync(user);
        await _pushNotificationService.Received(1).PushAsync(Arg.Is<PushNotification<UserPushNotification>>(n => n.Type == PushType.SyncVault && n.TargetId == user.Id));
    }

    [Fact]
    public async Task Run_UserNeverHadPremium_SetsProvidedStorageAndLicenseKey()
    {
        var user = new User { Id = Guid.NewGuid(), Premium = false, GatewayCustomerId = "cus_1" };
        CustomerSubscriptions(Subscription("sub_live", StripeConstants.SubscriptionStatus.Active, user.Id));

        await _command.Run(user);

        Assert.Equal((short)1, user.MaxStorageGb);
        Assert.False(string.IsNullOrWhiteSpace(user.LicenseKey));
    }

    [Fact]
    public async Task Run_UserHadPremium_KeepsStorageAndLicenseKey()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Premium = false,
            GatewayCustomerId = "cus_1",
            MaxStorageGb = 5,
            LicenseKey = "existing-license-key"
        };
        CustomerSubscriptions(Subscription("sub_live", StripeConstants.SubscriptionStatus.Active, user.Id));

        await _command.Run(user);

        Assert.Equal((short)5, user.MaxStorageGb);
        Assert.Equal("existing-license-key", user.LicenseKey);
    }

    [Theory]
    [InlineData(StripeConstants.SubscriptionStatus.Unpaid)]
    [InlineData(StripeConstants.SubscriptionStatus.PastDue)]
    [InlineData(StripeConstants.SubscriptionStatus.Incomplete)]
    public async Task Run_UnpaidSubscription_ReturnsItWithoutRestoringPremium(string status)
    {
        var user = new User { Id = Guid.NewGuid(), Premium = false, GatewayCustomerId = "cus_1" };
        CustomerSubscriptions(Subscription("sub_unpaid", status, user.Id));

        var result = await _command.Run(user);

        Assert.Equal("sub_unpaid", result?.Id);
        Assert.False(user.Premium);
        await _userService.DidNotReceiveWithAnyArgs().SaveUserAsync(default!);
    }

    [Fact]
    public async Task Run_ActiveAndNewerUnpaidSubscriptions_RestoresFromActiveOne()
    {
        var user = new User { Id = Guid.NewGuid(), Premium = false, GatewayCustomerId = "cus_1" };
        CustomerSubscriptions(
            Subscription("sub_unpaid", StripeConstants.SubscriptionStatus.Unpaid, user.Id),
            Subscription("sub_live", StripeConstants.SubscriptionStatus.Active, user.Id));

        var result = await _command.Run(user);

        Assert.Equal("sub_live", result?.Id);
        Assert.True(user.Premium);
        Assert.Equal("sub_live", user.GatewaySubscriptionId);
    }

    [Theory]
    [InlineData(StripeConstants.SubscriptionStatus.Canceled)]
    [InlineData(StripeConstants.SubscriptionStatus.IncompleteExpired)]
    public async Task Run_SubscriptionEnded_ReturnsNull(string status)
    {
        var user = new User { Id = Guid.NewGuid(), Premium = false, GatewayCustomerId = "cus_1" };
        CustomerSubscriptions(Subscription("sub_ended", status, user.Id));

        var result = await _command.Run(user);

        Assert.Null(result);
        await _userService.DidNotReceiveWithAnyArgs().SaveUserAsync(default!);
    }

    [Fact]
    public async Task Run_OnlyAnotherUsersSubscription_ReturnsNull()
    {
        var user = new User { Id = Guid.NewGuid(), Premium = false, GatewayCustomerId = "cus_1" };
        CustomerSubscriptions(Subscription("sub_other", StripeConstants.SubscriptionStatus.Active, Guid.NewGuid()));

        var result = await _command.Run(user);

        Assert.Null(result);
        Assert.False(user.Premium);
    }

    private void CustomerSubscriptions(params Subscription[] subscriptions) =>
        _stripeAdapter.ListSubscriptionsAsync(Arg.Is<SubscriptionListOptions>(options => options.Customer == "cus_1"))
            .Returns(new StripeList<Subscription> { Data = [.. subscriptions] });

    private static Subscription Subscription(string id, string status, Guid userId, DateTime? currentPeriodEnd = null) => new()
    {
        Id = id,
        Status = status,
        Metadata = new Dictionary<string, string> { [StripeConstants.MetadataKeys.UserId] = userId.ToString() },
        Items = new StripeList<SubscriptionItem>
        {
            Data = [new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd ?? DateTime.UtcNow.AddMonths(12) }]
        }
    };
}
