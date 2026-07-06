using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Commands;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Models;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Test.Billing.Mocks.Plans;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.AnnualUpgradeOffer.Commands;

public class RedeemAnnualUpgradeOfferCommandTests
{
    private readonly IGetAnnualUpgradeOfferQuery _getOfferQuery = Substitute.For<IGetAnnualUpgradeOfferQuery>();
    private readonly IPriceIncreaseScheduler _priceIncreaseScheduler = Substitute.For<IPriceIncreaseScheduler>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly RedeemAnnualUpgradeOfferCommand _command;

    public RedeemAnnualUpgradeOfferCommandTests()
    {
        _command = new RedeemAnnualUpgradeOfferCommand(
            Substitute.For<ILogger<RedeemAnnualUpgradeOfferCommand>>(),
            _getOfferQuery,
            _priceIncreaseScheduler,
            _pricingClient,
            _stripeAdapter);
    }

    private static Organization CreateOrganization(PlanType planType) => new()
    {
        Id = Guid.NewGuid(),
        PlanType = planType,
        GatewaySubscriptionId = "sub_123"
    };

    [Fact]
    public async Task Run_QueryReturnsNullOnRevalidation_ReturnsFailure_StripeNotMutated()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        _getOfferQuery.Run(organization).Returns((AnnualUpgradeOfferResult?)null);

        var result = await _command.Run(organization);

        Assert.True(result.IsT1);
        Assert.Equal("Offer is no longer available.", result.AsT1.Response);
        await _priceIncreaseScheduler.DidNotReceive().Release(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>());
        await _stripeAdapter.DidNotReceive().CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>());
    }

    [Fact]
    public async Task Run_NoExistingSchedule_ReleasesNothingMeaningful_CreatesTwoPhaseSchedule()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        _getOfferQuery.Run(organization).Returns(new AnnualUpgradeOfferResult(60m, 48m, 12m));

        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 10 }]
            }
        };
        _stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        var phase1 = new SubscriptionSchedulePhase
        {
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddYears(1),
            Items = [new SubscriptionSchedulePhaseItem { PriceId = monthlyPlan.PasswordManager.StripeSeatPlanId, Quantity = 10 }]
        };
        var schedule = new SubscriptionSchedule { Id = "sub_sched_new", Phases = [phase1] };
        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>()).Returns(schedule);

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);
        // Passing organization.Id (not null) is what drops the org's cohort assignment inside
        // Release -- Alex's 2026-07-02 decision that switching to annual also exits the cohort.
        await _priceIncreaseScheduler.Received(1).Release(subscription.CustomerId, subscription.Id, organization.Id);
        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(schedule.Id, Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
            o.EndBehavior == "release" &&
            o.Phases.Count == 2 &&
            o.Phases[1].Items.Any(i => i.Price == annualPlan.PasswordManager.StripeSeatPlanId && i.Quantity == 10)));
    }

    [Fact]
    public async Task Run_ScheduleUpdateFails_ReleasesOrphanedScheduleAndReturnsUnhandled()
    {
        var organization = CreateOrganization(PlanType.EnterpriseMonthly);
        _getOfferQuery.Run(organization).Returns(new AnnualUpgradeOfferResult(600m, 480m, 120m));

        var monthlyPlan = new EnterprisePlan(false);
        var annualPlan = new EnterprisePlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(annualPlan);

        var subscription = new Subscription
        {
            Id = "sub_456",
            CustomerId = "cus_456",
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 5 }]
            }
        };
        _stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        var phase1 = new SubscriptionSchedulePhase
        {
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddYears(1),
            Items = [new SubscriptionSchedulePhaseItem { PriceId = monthlyPlan.PasswordManager.StripeSeatPlanId, Quantity = 5 }]
        };
        var schedule = new SubscriptionSchedule { Id = "sub_sched_fail", Phases = [phase1] };
        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>()).Returns(schedule);
        _stripeAdapter.UpdateSubscriptionScheduleAsync(schedule.Id, Arg.Any<SubscriptionScheduleUpdateOptions>())
            .Returns<SubscriptionSchedule>(_ => throw new StripeException { StripeError = new StripeError { Code = "api_error" } });

        // BaseBillingCommand.HandleAsync catches any StripeException not in ErrorCodes.InputErrors()
        // and returns it boxed as an Unhandled (T3) result rather than letting it propagate to the
        // caller -- see BaseBillingCommand.cs's final `catch (StripeException stripeException)` block.
        // So the command never throws here; it returns a non-success result instead. The two
        // behavioral guarantees under test are: the orphaned schedule gets released exactly once,
        // and the command does not report success.
        var result = await _command.Run(organization);

        Assert.False(result.IsT0);
        Assert.True(result.IsT3);
        Assert.IsType<StripeException>(result.AsT3.Exception);

        await _stripeAdapter.Received(1).ReleaseSubscriptionScheduleAsync(schedule.Id);
    }
}
