using Bit.Billing.Jobs;
using Bit.Billing.Services;
using Bit.Core;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Pricing.Premium;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bitwarden.Server.Sdk.Features;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;
using Stripe;
using Xunit;
using Plan = Bit.Core.Billing.Pricing.Premium.Plan;

namespace Bit.Billing.Test.Jobs;

public class ReconcilePremiumJobTests
{
    private const string PremiumPriceId = "premium-annually-2026";

    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IPushNotificationAdapter _pushNotificationAdapter = Substitute.For<IPushNotificationAdapter>();
    private readonly IFeatureService _featureService = Substitute.For<IFeatureService>();
    private readonly ReconcilePremiumJob _sut;

    public ReconcilePremiumJobTests()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM46168_ReconcilePremium).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM46168_ReconcilePremiumApply).Returns(true);
        _pricingClient.ListPremiumPlans().Returns([new Plan { Seat = new Purchasable { StripePriceId = PremiumPriceId } }]);
        _stripeAdapter.ListSubscriptionsAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(new StripeList<Subscription> { Data = [] });

        _sut = new ReconcilePremiumJob(
            _stripeAdapter,
            _pricingClient,
            _userRepository,
            _userService,
            _pushNotificationAdapter,
            _featureService,
            GlobalSettingsInRegion("US"),
            Substitute.For<ILogger<ReconcilePremiumJob>>());
    }

    [Fact]
    public async Task Execute_FeatureFlagDisabled_DoesNothing()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM46168_ReconcilePremium).Returns(false);

        await _sut.Execute(CreateContext());

        await _pricingClient.DidNotReceive().ListPremiumPlans();
        await _stripeAdapter.DidNotReceiveWithAnyArgs().ListSubscriptionsAsync(default);
    }

    [Fact]
    public async Task Execute_PremiumOffWithLiveCurrentSubscription_EnablesPremiumAndNotifies()
    {
        var periodEnd = DateTime.UtcNow.AddMonths(11);
        var user = UserWith(premium: false, gatewaySubscriptionId: "sub_current");
        LiveSubscriptions(Subscription("sub_current", user.Id, periodEnd));
        Users(user);
        _userRepository.GetByIdAsync(user.Id).Returns(UserWith(premium: true, gatewaySubscriptionId: "sub_current", id: user.Id));

        await _sut.Execute(CreateContext());

        await _userService.Received(1).EnablePremiumAsync(user.Id, periodEnd);
        await _pushNotificationAdapter.Received(1).NotifyPremiumStatusChangedAsync(Arg.Is<User>(u => u.Id == user.Id));
    }

    [Fact]
    public async Task Execute_PremiumOffWithLiveUnlinkedSubscription_EnablesPremiumFromThatSubscription()
    {
        var periodEnd = DateTime.UtcNow.AddMonths(11);
        var user = UserWith(premium: false, gatewaySubscriptionId: "sub_canceled");
        LiveSubscriptions(Subscription("sub_unlinked", user.Id, periodEnd));
        Users(user);

        await _sut.Execute(CreateContext());

        await _userService.Received(1).EnablePremiumAsync(user.Id, periodEnd);
    }

    [Fact]
    public async Task Execute_ApplyFlagDisabled_WritesNothing()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM46168_ReconcilePremiumApply).Returns(false);
        var off = UserWith(premium: false, gatewaySubscriptionId: "sub_a");
        var lagging = UserWith(premium: true, gatewaySubscriptionId: "sub_b", premiumExpirationDate: DateTime.UtcNow);
        LiveSubscriptions(
            Subscription("sub_a", off.Id, DateTime.UtcNow.AddMonths(11)),
            Subscription("sub_b", lagging.Id, DateTime.UtcNow.AddMonths(11)));
        Users(off, lagging);

        await _sut.Execute(CreateContext());

        await _userService.DidNotReceiveWithAnyArgs().EnablePremiumAsync(default, default);
        await _userService.DidNotReceiveWithAnyArgs().UpdatePremiumExpirationAsync(default, default);
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyPremiumStatusChangedAsync(default!);
    }

    [Fact]
    public async Task Execute_PremiumOnWithLaggingExpiration_CorrectsExpirationToCurrentSubscription()
    {
        var periodEnd = DateTime.UtcNow.AddMonths(11);
        var user = UserWith(premium: true, gatewaySubscriptionId: "sub_current", premiumExpirationDate: DateTime.UtcNow.AddDays(-3));
        LiveSubscriptions(Subscription("sub_current", user.Id, periodEnd));
        Users(user);

        await _sut.Execute(CreateContext());

        await _userService.Received(1).UpdatePremiumExpirationAsync(user.Id, periodEnd);
        await _userService.DidNotReceiveWithAnyArgs().EnablePremiumAsync(default, default);
    }

    [Fact]
    public async Task Execute_PremiumOnWithDuplicateSubscriptions_OnlyReports()
    {
        var periodEnd = DateTime.UtcNow.AddMonths(11);
        var user = UserWith(premium: true, gatewaySubscriptionId: "sub_new", premiumExpirationDate: periodEnd);
        LiveSubscriptions(
            Subscription("sub_old", user.Id, DateTime.UtcNow.AddMonths(2)),
            Subscription("sub_new", user.Id, periodEnd));
        Users(user);

        await _sut.Execute(CreateContext());

        await _userService.DidNotReceiveWithAnyArgs().EnablePremiumAsync(default, default);
        await _userService.DidNotReceiveWithAnyArgs().UpdatePremiumExpirationAsync(default, default);
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CancelSubscriptionAsync(default, default);
    }

    [Fact]
    public async Task Execute_MultiplePages_ReadsEveryPage()
    {
        var first = UserWith(premium: false, gatewaySubscriptionId: "sub_1");
        var second = UserWith(premium: false, gatewaySubscriptionId: "sub_2");
        _stripeAdapter.ListSubscriptionsAsync(Arg.Is<SubscriptionListOptions>(options =>
                options.Status == StripeConstants.SubscriptionStatus.Active && options.StartingAfter == null))
            .Returns(new StripeList<Subscription> { Data = [Subscription("sub_1", first.Id, DateTime.UtcNow.AddMonths(6))], HasMore = true });
        _stripeAdapter.ListSubscriptionsAsync(Arg.Is<SubscriptionListOptions>(options =>
                options.Status == StripeConstants.SubscriptionStatus.Active && options.StartingAfter == "sub_1"))
            .Returns(new StripeList<Subscription> { Data = [Subscription("sub_2", second.Id, DateTime.UtcNow.AddMonths(6))], HasMore = false });
        Users(first, second);

        await _sut.Execute(CreateContext());

        await _userService.Received(1).EnablePremiumAsync(first.Id, Arg.Any<DateTime?>());
        await _userService.Received(1).EnablePremiumAsync(second.Id, Arg.Any<DateTime?>());
    }

    [Fact]
    public async Task Execute_OneUserFails_ContinuesWithOthers()
    {
        var failing = UserWith(premium: false, gatewaySubscriptionId: "sub_fail");
        var healthy = UserWith(premium: false, gatewaySubscriptionId: "sub_ok");
        LiveSubscriptions(
            Subscription("sub_fail", failing.Id, DateTime.UtcNow.AddMonths(6)),
            Subscription("sub_ok", healthy.Id, DateTime.UtcNow.AddMonths(6)));
        Users(failing, healthy);
        _userService.EnablePremiumAsync(failing.Id, Arg.Any<DateTime?>()).ThrowsAsync(new Exception("database unavailable"));

        await _sut.Execute(CreateContext());

        await _userService.Received(1).EnablePremiumAsync(healthy.Id, Arg.Any<DateTime?>());
    }

    [Fact]
    public async Task Execute_UserBatchReadFails_ContinuesWithNextBatch()
    {
        var users = Enumerable.Range(0, 1001).Select(index => UserWith(premium: false, gatewaySubscriptionId: $"sub_{index}")).ToList();
        LiveSubscriptions([.. users.Select(user => Subscription(user.GatewaySubscriptionId!, user.Id, DateTime.UtcNow.AddMonths(6)))]);
        _userRepository.GetManyAsync(Arg.Any<IEnumerable<Guid>>()).Returns(
            _ => throw new Exception("database unavailable"),
            callInfo => users.Where(user => callInfo.Arg<IEnumerable<Guid>>().Contains(user.Id)));

        await _sut.Execute(CreateContext());

        await _userRepository.Received(2).GetManyAsync(Arg.Any<IEnumerable<Guid>>());
        await _userService.Received(1).EnablePremiumAsync(Arg.Any<Guid>(), Arg.Any<DateTime?>());
        await _userService.Received(1).EnablePremiumAsync(users[^1].Id, Arg.Any<DateTime?>());
    }

    [Fact]
    public async Task Execute_SubscriptionInAnotherRegion_IsIgnored()
    {
        var user = UserWith(premium: false, gatewaySubscriptionId: "sub_eu");
        LiveSubscriptions(Subscription("sub_eu", user.Id, DateTime.UtcNow.AddMonths(6), region: "EU"));
        Users(user);

        await _sut.Execute(CreateContext());

        await _userRepository.DidNotReceiveWithAnyArgs().GetManyAsync(default!);
        await _userService.DidNotReceiveWithAnyArgs().EnablePremiumAsync(default, default);
    }

    [Fact]
    public async Task Execute_SubscriptionWithoutUserMetadata_IsIgnored()
    {
        LiveSubscriptions(new Subscription
        {
            Id = "sub_org",
            Customer = CustomerInRegion("US"),
            Metadata = new Dictionary<string, string> { ["organizationId"] = Guid.NewGuid().ToString() },
            Items = new StripeList<SubscriptionItem> { Data = [new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddMonths(6) }] }
        });

        await _sut.Execute(CreateContext());

        await _userRepository.DidNotReceiveWithAnyArgs().GetManyAsync(default!);
    }

    private void Users(params User[] users) =>
        _userRepository.GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo => users.Where(user => callInfo.Arg<IEnumerable<Guid>>().Contains(user.Id)));

    private void LiveSubscriptions(params Subscription[] subscriptions) =>
        _stripeAdapter.ListSubscriptionsAsync(Arg.Is<SubscriptionListOptions>(options =>
                options.Price == PremiumPriceId && options.Status == StripeConstants.SubscriptionStatus.Active))
            .Returns(new StripeList<Subscription> { Data = [.. subscriptions], HasMore = false });

    private static Subscription Subscription(string id, Guid userId, DateTime currentPeriodEnd, string region = "US") => new()
    {
        Id = id,
        Customer = CustomerInRegion(region),
        Status = StripeConstants.SubscriptionStatus.Active,
        Metadata = new Dictionary<string, string> { [StripeConstants.MetadataKeys.UserId] = userId.ToString() },
        Items = new StripeList<SubscriptionItem> { Data = [new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd }] }
    };

    private static User UserWith(bool premium, string gatewaySubscriptionId, DateTime? premiumExpirationDate = null, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Premium = premium,
        GatewaySubscriptionId = gatewaySubscriptionId,
        PremiumExpirationDate = premiumExpirationDate
    };

    private static Customer CustomerInRegion(string region) =>
        new() { Metadata = new Dictionary<string, string> { [StripeConstants.MetadataKeys.Region] = region } };

    private static GlobalSettings GlobalSettingsInRegion(string region)
    {
        var globalSettings = new GlobalSettings();
        globalSettings.BaseServiceUri = new GlobalSettings.BaseServiceUriSettings(globalSettings) { CloudRegion = region };
        return globalSettings;
    }

    private static IJobExecutionContext CreateContext()
    {
        var context = Substitute.For<IJobExecutionContext>();
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }
}
