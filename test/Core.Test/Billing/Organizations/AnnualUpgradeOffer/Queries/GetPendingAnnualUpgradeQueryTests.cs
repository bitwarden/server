using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Queries;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Services;
using Bit.Core.Test.Billing.Mocks.Plans;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.AnnualUpgradeOffer.Queries;

using static StripeConstants;

public class GetPendingAnnualUpgradeQueryTests
{
    private readonly IFeatureService _featureService = Substitute.For<IFeatureService>();
    private readonly IGetChurnOfferCohortMembershipQuery _getChurnOfferCohortMembershipQuery =
        Substitute.For<IGetChurnOfferCohortMembershipQuery>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly ILogger<GetPendingAnnualUpgradeQuery> _logger =
        Substitute.For<ILogger<GetPendingAnnualUpgradeQuery>>();
    private readonly GetPendingAnnualUpgradeQuery _query;

    public GetPendingAnnualUpgradeQueryTests()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });
        _query = new GetPendingAnnualUpgradeQuery(
            _logger, _featureService, _getChurnOfferCohortMembershipQuery, _pricingClient, _stripeAdapter);
    }

    private static Organization CreateOrganization(PlanType planType) => new()
    {
        Id = Guid.NewGuid(),
        PlanType = planType,
        GatewaySubscriptionId = "sub_123"
    };

    private Subscription SetupSubscription(Organization organization)
    {
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Status = SubscriptionStatus.Active,
            Items = new StripeList<SubscriptionItem> { Data = [] }
        };
        _stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        return subscription;
    }

    [Fact]
    public async Task Run_FlagDisabled_ReturnsNull_WithoutLookups()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(false);

        var result = await _query.Run(CreateOrganization(PlanType.TeamsMonthly));

        Assert.Null(result);
        await _getChurnOfferCohortMembershipQuery.DidNotReceive().Run(Arg.Any<Organization>());
        await _stripeAdapter.DidNotReceive().GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
    }

    [Fact]
    public async Task Run_OrgInChurnOfferCohort_ReturnsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        _getChurnOfferCohortMembershipQuery.Run(organization).Returns(
            new ChurnOfferCohortMembership(
                new OrganizationPlanMigrationCohortAssignment { Id = Guid.NewGuid(), OrganizationId = organization.Id, CohortId = Guid.NewGuid() },
                new OrganizationPlanMigrationCohort { Id = Guid.NewGuid(), Name = "cohort", IsActive = true, ChurnDiscountCouponCode = "coupon" }));

        var result = await _query.Run(organization);

        Assert.Null(result);
    }

    [Fact]
    public async Task Run_AnnualPlanType_ReturnsNull()
    {
        var result = await _query.Run(CreateOrganization(PlanType.TeamsAnnually));

        Assert.Null(result);
        await _stripeAdapter.DidNotReceive().GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
    }

    [Fact]
    public async Task Run_NoRedeemedSchedule_ReturnsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        SetupSubscription(organization);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(new TeamsPlan(true));
        // Default ListSubscriptionSchedulesAsync returns empty in the constructor.

        var result = await _query.Run(organization);

        Assert.Null(result);
    }

    [Fact]
    public async Task Run_RedeemedSchedule_ReturnsTargetPlanAndLineItems()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var subscription = SetupSubscription(organization);

        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        var annualSeatPriceId = annualPlan.PasswordManager.StripeSeatPlanId;
        var renewalDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        SubscriptionId = subscription.Id,
                        Status = SubscriptionScheduleStatus.Active,
                        Phases =
                        [
                            new SubscriptionSchedulePhase
                            {
                                StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                                Items = [new SubscriptionSchedulePhaseItem { PriceId = "price_monthly", Quantity = 5 }]
                            },
                            new SubscriptionSchedulePhase
                            {
                                StartDate = renewalDate,
                                Items = [new SubscriptionSchedulePhaseItem { PriceId = annualSeatPriceId, Quantity = 5 }]
                            }
                        ]
                    }
                ]
            });

        _stripeAdapter.GetPriceAsync(annualSeatPriceId, Arg.Any<PriceGetOptions>())
            .Returns(new Price
            {
                Nickname = "Teams (Annually) Seat",
                UnitAmountDecimal = 4800,
                ProductId = "prod_teams",
                Recurring = new PriceRecurring { Interval = "year" }
            });

        var result = await _query.Run(organization);

        Assert.NotNull(result);
        Assert.Equal(annualPlan.Type, result.Plan.Type);
        Assert.Equal(renewalDate, result.EffectiveDate);
        var lineItem = Assert.Single(result.LineItems);
        Assert.Equal("Teams (Annually) Seat", lineItem.Name);
        Assert.Equal(48m, lineItem.Amount);
        Assert.Equal(5, lineItem.Quantity);
        Assert.Equal("year", lineItem.Interval);
    }
}
