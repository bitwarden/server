using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Queries;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.PlanMigration.Queries;

using static StripeConstants;

public class GetChurnMitigationOfferQueryTests
{
    private const string ChurnCouponCode = "churn-15-percent-once";

    private readonly IOrganizationPlanMigrationCohortAssignmentRepository _assignmentRepository =
        Substitute.For<IOrganizationPlanMigrationCohortAssignmentRepository>();
    private readonly IOrganizationPlanMigrationCohortRepository _cohortRepository =
        Substitute.For<IOrganizationPlanMigrationCohortRepository>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly GetChurnMitigationOfferQuery _query;

    public GetChurnMitigationOfferQueryTests()
    {
        _query = new GetChurnMitigationOfferQuery(
            _assignmentRepository,
            _cohortRepository,
            _stripeAdapter,
            Substitute.For<ILogger<GetChurnMitigationOfferQuery>>());
    }

    [Fact]
    public async Task Run_NoAssignment_ReturnsNull()
    {
        var organization = CreateOrganization();
        _assignmentRepository.GetByOrganizationIdAsync(organization.Id)
            .Returns((OrganizationPlanMigrationCohortAssignment?)null);

        var result = await _query.Run(organization);

        Assert.Null(result);
        await _cohortRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task Run_CohortInactive_ReturnsNull()
    {
        var organization = CreateOrganization();
        SetupChurnOnlyCohort(organization, isActive: false);

        var result = await _query.Run(organization);

        Assert.Null(result);
    }

    [Fact]
    public async Task Run_NullChurnCouponCode_ReturnsNull()
    {
        var organization = CreateOrganization();
        SetupChurnOnlyCohort(organization, churnCouponCode: null);

        var result = await _query.Run(organization);

        Assert.Null(result);
    }

    [Fact]
    public async Task Run_MigrationCohort_SchedulePhase1Active_CouponNotOnPhase2_ReturnsOffer()
    {
        var organization = CreateOrganization();
        SetupMigrationCohort(organization);

        var subscription = CreateSubscription();
        SetupGetSubscription(organization, subscription);
        SetupActiveSchedule(subscription, phase1Active: true);
        SetupGetCoupon(CreatePercentOffCoupon());

        var result = await _query.Run(organization);

        Assert.NotNull(result);
        Assert.Equal(ChurnCouponCode, result.CouponId);
        Assert.Equal(15m, result.PercentOff);
        Assert.Equal(CouponDurations.Once, result.Duration);
    }

    [Fact]
    public async Task Run_MigrationCohort_AmountOffCoupon_PercentOffIsNull_ResultStillConstructs()
    {
        var organization = CreateOrganization();
        SetupMigrationCohort(organization);

        var subscription = CreateSubscription();
        SetupGetSubscription(organization, subscription);
        SetupActiveSchedule(subscription, phase1Active: true);
        SetupGetCoupon(new Coupon
        {
            Id = ChurnCouponCode,
            AmountOff = 500,
            PercentOff = null,
            Duration = CouponDurations.Once,
            Name = "Amount-off coupon",
            Valid = true
        });

        var result = await _query.Run(organization);

        Assert.NotNull(result);
        Assert.Null(result.PercentOff);
        Assert.Equal(ChurnCouponCode, result.CouponId);
    }

    [Fact]
    public async Task Run_MigrationCohort_NoActiveSchedule_ReturnsNull()
    {
        var organization = CreateOrganization();
        SetupMigrationCohort(organization);

        var subscription = CreateSubscription();
        SetupGetSubscription(organization, subscription);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        var result = await _query.Run(organization);

        Assert.Null(result);
        await _stripeAdapter.DidNotReceive().GetCouponAsync(Arg.Any<string>(), Arg.Any<CouponGetOptions>());
    }

    [Fact]
    public async Task Run_MigrationCohort_CurrentPhaseIsPhase2_ReturnsNull()
    {
        var organization = CreateOrganization();
        SetupMigrationCohort(organization);

        var subscription = CreateSubscription();
        SetupGetSubscription(organization, subscription);
        SetupActiveSchedule(subscription, phase1Active: false);

        var result = await _query.Run(organization);

        Assert.Null(result);
        await _stripeAdapter.DidNotReceive().GetCouponAsync(Arg.Any<string>(), Arg.Any<CouponGetOptions>());
    }

    [Fact]
    public async Task Run_MigrationCohort_CouponAlreadyOnPhase2_ReturnsNull()
    {
        var organization = CreateOrganization();
        SetupMigrationCohort(organization);

        var subscription = CreateSubscription();
        SetupGetSubscription(organization, subscription);

        var schedule = BuildSchedule(subscription.Id, phase1Active: true,
            phase2Discounts: [new SubscriptionSchedulePhaseDiscount { CouponId = ChurnCouponCode }]);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

        var result = await _query.Run(organization);

        Assert.Null(result);
        await _stripeAdapter.DidNotReceive().GetCouponAsync(Arg.Any<string>(), Arg.Any<CouponGetOptions>());
    }

    [Fact]
    public async Task Run_MigrationCohort_CouponsGetThrows_ReturnsNullAndLogsWarning()
    {
        var logger = Substitute.For<ILogger<GetChurnMitigationOfferQuery>>();
        var query = new GetChurnMitigationOfferQuery(
            _assignmentRepository, _cohortRepository, _stripeAdapter, logger);

        var organization = CreateOrganization();
        SetupMigrationCohort(organization);

        var subscription = CreateSubscription();
        SetupGetSubscription(organization, subscription);
        SetupActiveSchedule(subscription, phase1Active: true);

        _stripeAdapter.GetCouponAsync(ChurnCouponCode, Arg.Any<CouponGetOptions>())
            .Returns<Coupon>(_ => throw new StripeException { StripeError = new StripeError { Code = "api_error" } });

        var result = await query.Run(organization);

        Assert.Null(result);
        logger.ReceivedWithAnyArgs().Log<object>(LogLevel.Warning, default, default!, default, default!);
    }

    [Fact]
    public async Task Run_MigrationCohort_CouponsGet404_ReturnsNullAndLogsWarning()
    {
        var logger = Substitute.For<ILogger<GetChurnMitigationOfferQuery>>();
        var query = new GetChurnMitigationOfferQuery(
            _assignmentRepository, _cohortRepository, _stripeAdapter, logger);

        var organization = CreateOrganization();
        SetupMigrationCohort(organization);

        var subscription = CreateSubscription();
        SetupGetSubscription(organization, subscription);
        SetupActiveSchedule(subscription, phase1Active: true);

        _stripeAdapter.GetCouponAsync(ChurnCouponCode, Arg.Any<CouponGetOptions>())
            .Returns<Coupon>(_ => throw new StripeException
            {
                StripeError = new StripeError { Code = ErrorCodes.ResourceMissing }
            });

        var result = await query.Run(organization);

        Assert.Null(result);
        logger.ReceivedWithAnyArgs().Log<object>(LogLevel.Warning, default, default!, default, default!);
    }

    [Fact]
    public async Task Run_ChurnOnlyCohort_OnceCouponNeverRedeemed_ReturnsOffer()
    {
        var organization = CreateOrganization();
        SetupChurnOnlyCohort(organization);

        SetupGetCoupon(CreatePercentOffCoupon(duration: CouponDurations.Once));

        var subscription = CreateSubscription();
        SetupGetSubscription(organization, subscription);

        var result = await _query.Run(organization);

        Assert.NotNull(result);
        Assert.Equal(CouponDurations.Once, result.Duration);
    }

    [Fact]
    public async Task Run_ChurnOnlyCohort_RepeatingCouponNotOnSubscription_ReturnsOffer()
    {
        var organization = CreateOrganization();
        SetupChurnOnlyCohort(organization);

        SetupGetCoupon(CreatePercentOffCoupon(duration: CouponDurations.Repeating, durationInMonths: 3));

        var subscription = CreateSubscription();
        SetupGetSubscription(organization, subscription);

        var result = await _query.Run(organization);

        Assert.NotNull(result);
        Assert.Equal(CouponDurations.Repeating, result.Duration);
        Assert.Equal(3, result.DurationInMonths);
    }

    [Fact]
    public async Task Run_ChurnOnlyCohort_ForeverCouponNotOnSubscription_ReturnsOffer()
    {
        var organization = CreateOrganization();
        SetupChurnOnlyCohort(organization);

        SetupGetCoupon(CreatePercentOffCoupon(duration: CouponDurations.Forever));

        var subscription = CreateSubscription();
        SetupGetSubscription(organization, subscription);

        var result = await _query.Run(organization);

        Assert.NotNull(result);
        Assert.Equal(CouponDurations.Forever, result.Duration);
    }

    [Fact]
    public async Task Run_ChurnOnlyCohort_OnceCouponAlreadyRedeemed_ReturnsNull()
    {
        var organization = CreateOrganization();
        SetupChurnOnlyCohort(organization, churnDiscountAppliedDate: DateTime.UtcNow.AddDays(-1));

        SetupGetCoupon(CreatePercentOffCoupon(duration: CouponDurations.Once));

        var result = await _query.Run(organization);

        Assert.Null(result);
        // Once the per-assignment guard short-circuits, the subscription is not fetched.
        await _stripeAdapter.DidNotReceive().GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
    }

    [Fact]
    public async Task Run_ChurnOnlyCohort_CouponOnSubscriptionDiscounts_ReturnsNull()
    {
        var organization = CreateOrganization();
        SetupChurnOnlyCohort(organization);

        SetupGetCoupon(CreatePercentOffCoupon());

        var subscription = CreateSubscription(subscriptionDiscounts:
        [
            new Discount { Coupon = new Coupon { Id = ChurnCouponCode } }
        ]);
        SetupGetSubscription(organization, subscription);

        var result = await _query.Run(organization);

        Assert.Null(result);
    }

    [Fact]
    public async Task Run_ChurnOnlyCohort_CouponOnCustomerDiscount_ReturnsNull()
    {
        var organization = CreateOrganization();
        SetupChurnOnlyCohort(organization);

        SetupGetCoupon(CreatePercentOffCoupon());

        var subscription = CreateSubscription(customerDiscount: new Discount
        {
            Coupon = new Coupon { Id = ChurnCouponCode }
        });
        SetupGetSubscription(organization, subscription);

        var result = await _query.Run(organization);

        Assert.Null(result);
    }

    // ---- helpers ----

    private static Organization CreateOrganization() => new()
    {
        Id = Guid.NewGuid(),
        GatewaySubscriptionId = "sub_123"
    };

    private static Subscription CreateSubscription(
        Discount? customerDiscount = null,
        List<Discount>? subscriptionDiscounts = null)
    {
        return new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Customer = new Customer { Id = "cus_123", Discount = customerDiscount },
            Discounts = subscriptionDiscounts,
            Items = new StripeList<SubscriptionItem> { Data = [] }
        };
    }

    private void SetupGetSubscription(Organization organization, Subscription subscription)
    {
        _stripeAdapter
            .GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
    }

    private void SetupGetCoupon(Coupon coupon)
    {
        _stripeAdapter.GetCouponAsync(coupon.Id, Arg.Any<CouponGetOptions>())
            .Returns(coupon);
    }

    private static Coupon CreatePercentOffCoupon(
        string duration = CouponDurations.Once,
        long? durationInMonths = null) =>
        new()
        {
            Id = ChurnCouponCode,
            PercentOff = 15m,
            Duration = duration,
            DurationInMonths = durationInMonths,
            Name = "Churn 15% off",
            Valid = true
        };

    private void SetupActiveSchedule(Subscription subscription, bool phase1Active)
    {
        var schedule = BuildSchedule(subscription.Id, phase1Active);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });
    }

    private static SubscriptionSchedule BuildSchedule(
        string subscriptionId,
        bool phase1Active,
        List<SubscriptionSchedulePhaseDiscount>? phase2Discounts = null)
    {
        var phase1Start = phase1Active ? DateTime.UtcNow.AddDays(-30) : DateTime.UtcNow.AddYears(-1);
        var phase1End = phase1Active ? DateTime.UtcNow.AddYears(1) : DateTime.UtcNow.AddDays(-1);

        var phase1 = new SubscriptionSchedulePhase
        {
            StartDate = phase1Start,
            EndDate = phase1End,
            Items = []
        };
        var phase2 = new SubscriptionSchedulePhase
        {
            StartDate = phase1End,
            EndDate = phase1End.AddYears(1),
            Items = [],
            Discounts = phase2Discounts
        };

        var currentPhase = phase1Active
            ? new SubscriptionScheduleCurrentPhase { StartDate = phase1Start, EndDate = phase1End }
            : new SubscriptionScheduleCurrentPhase { StartDate = phase1End, EndDate = phase1End.AddYears(1) };

        return new SubscriptionSchedule
        {
            Id = "sub_sched_123",
            SubscriptionId = subscriptionId,
            Status = SubscriptionScheduleStatus.Active,
            Phases = [phase1, phase2],
            CurrentPhase = currentPhase
        };
    }

    private void SetupMigrationCohort(Organization organization)
    {
        var cohortId = Guid.NewGuid();
        _assignmentRepository.GetByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationPlanMigrationCohortAssignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = organization.Id,
                CohortId = cohortId
            });
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "migration-cohort",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            ChurnDiscountCouponCode = ChurnCouponCode,
            IsActive = true
        });
    }

    private void SetupChurnOnlyCohort(
        Organization organization,
        bool isActive = true,
        string? churnCouponCode = ChurnCouponCode,
        DateTime? churnDiscountAppliedDate = null)
    {
        var cohortId = Guid.NewGuid();
        _assignmentRepository.GetByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationPlanMigrationCohortAssignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = organization.Id,
                CohortId = cohortId,
                ChurnDiscountAppliedDate = churnDiscountAppliedDate
            });
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "churn-only-cohort",
            MigrationPathId = null,
            ChurnDiscountCouponCode = churnCouponCode,
            IsActive = isActive
        });
    }
}
