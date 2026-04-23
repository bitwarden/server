using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions.Commands;
using Bit.Core.Entities;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;
using static Bit.Core.Billing.Constants.StripeConstants;

namespace Bit.Core.Test.Billing.Subscriptions.Commands;

public class ReinstateSubscriptionCommandTests
{
    private readonly IFeatureService _featureService = Substitute.For<IFeatureService>();
    private readonly IPriceIncreaseScheduler _priceIncreaseScheduler = Substitute.For<IPriceIncreaseScheduler>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly ILogger<ReinstateSubscriptionCommand> _logger = Substitute.For<ILogger<ReinstateSubscriptionCommand>>();
    private readonly ReinstateSubscriptionCommand _command;

    public ReinstateSubscriptionCommandTests()
    {
        _command = new ReinstateSubscriptionCommand(_logger, _stripeAdapter, _featureService, _priceIncreaseScheduler);
    }

    [Fact]
    public async Task Run_SubscriptionNotPendingCancellation_ReturnsBadRequest()
    {
        var user = new User { GatewaySubscriptionId = "sub_1" };

        _stripeAdapter.GetSubscriptionAsync("sub_1")
            .Returns(new Subscription { Status = SubscriptionStatus.Active, CancelAt = null });

        var result = await _command.Run(user);

        Assert.True(result.IsT1);
        Assert.Equal("Subscription is not pending cancellation.", result.AsT1.Response);
    }

    [Fact]
    public async Task Run_FlagOff_FallsThroughToStandardReinstate_NoScheduleCheck()
    {
        var user = new User { GatewaySubscriptionId = "sub_1" };

        _stripeAdapter.GetSubscriptionAsync("sub_1")
            .Returns(new Subscription
            {
                Id = "sub_1",
                Status = SubscriptionStatus.Active,
                CancelAt = DateTime.UtcNow.AddDays(30)
            });

        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(false);

        var result = await _command.Run(user);

        Assert.True(result.IsT0);
        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>());
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync("sub_1",
            Arg.Is<SubscriptionUpdateOptions>(o => o.CancelAtPeriodEnd == false));
    }

    [Fact]
    public async Task Run_FlagOn_NoSchedule_FallsThroughToStandardReinstate()
    {
        var user = new User { GatewaySubscriptionId = "sub_1" };

        _stripeAdapter.GetSubscriptionAsync("sub_1")
            .Returns(new Subscription
            {
                Id = "sub_1",
                Status = SubscriptionStatus.Active,
                CancelAt = DateTime.UtcNow.AddDays(30),
                CustomerId = "cus_1",
                Metadata = new Dictionary<string, string> { ["userId"] = user.Id.ToString() },
                Items = new StripeList<SubscriptionItem> { Data = [] }
            });

        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

        var result = await _command.Run(user);

        Assert.True(result.IsT0);
        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .UpdateSubscriptionScheduleAsync(Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>());
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync("sub_1",
            Arg.Is<SubscriptionUpdateOptions>(o => o.CancelAtPeriodEnd == false));
    }

    [Fact]
    public async Task Run_FlagOn_NoSchedule_CancelledDuringDeferredPriceIncrease_RecreatesScheduleAndClearsFlag()
    {
        var user = new User { GatewaySubscriptionId = "sub_1" };

        _stripeAdapter.GetSubscriptionAsync("sub_1")
            .Returns(new Subscription
            {
                Id = "sub_1",
                Status = SubscriptionStatus.Active,
                CancelAt = DateTime.UtcNow.AddDays(30),
                CustomerId = "cus_1",
                Metadata = new Dictionary<string, string>
                {
                    ["userId"] = user.Id.ToString(),
                    [MetadataKeys.CancelledDuringDeferredPriceIncrease] = "true"
                },
                Items = new StripeList<SubscriptionItem> { Data = [] }
            });

        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

        var result = await _command.Run(user);

        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync("sub_1",
            Arg.Is<SubscriptionUpdateOptions>(o =>
                o.CancelAtPeriodEnd == false &&
                o.Metadata[MetadataKeys.CancelledDuringDeferredPriceIncrease] == ""));
        await _priceIncreaseScheduler.Received(1).Schedule(Arg.Any<Subscription>());
    }

}
