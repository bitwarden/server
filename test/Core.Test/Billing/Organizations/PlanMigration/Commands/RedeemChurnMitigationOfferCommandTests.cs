using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.PlanMigration.Commands;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Queries;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.PlanMigration.Commands;

using static StripeConstants;

public class RedeemChurnMitigationOfferCommandTests
{
    private const string ChurnCouponCode = "churn-15-percent-once";

    private readonly IGetChurnMitigationOfferQuery _getOfferQuery =
        Substitute.For<IGetChurnMitigationOfferQuery>();
    private readonly IOrganizationPlanMigrationCohortAssignmentRepository _assignmentRepository =
        Substitute.For<IOrganizationPlanMigrationCohortAssignmentRepository>();
    private readonly IOrganizationPlanMigrationCohortRepository _cohortRepository =
        Substitute.For<IOrganizationPlanMigrationCohortRepository>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly RedeemChurnMitigationOfferCommand _command;

    public RedeemChurnMitigationOfferCommandTests()
    {
        _command = new RedeemChurnMitigationOfferCommand(
            Substitute.For<ILogger<RedeemChurnMitigationOfferCommand>>(),
            _getOfferQuery,
            _assignmentRepository,
            _cohortRepository,
            _stripeAdapter);
    }

    [Fact]
    public async Task Run_QueryReturnsNullOnRevalidation_ReturnsFailure_StripeNotMutated_DbNotWritten()
    {
        var organization = CreateOrganization();
        _getOfferQuery.Run(organization).Returns((ChurnMitigationOfferResult?)null);

        var result = await _command.Run(organization);

        Assert.True(result.IsT1);
        Assert.Equal("Offer is no longer available.", result.AsT1.Response);

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionScheduleAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>());
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
        await _assignmentRepository.DidNotReceive().ReplaceAsync(
            Arg.Any<OrganizationPlanMigrationCohortAssignment>());
    }

    [Fact]
    public async Task Run_MigrationCohort_AppendsCouponToPhase2_WritesAppliedDate_WithinTolerance()
    {
        var organization = CreateOrganization();
        SetupOfferEligible();
        var assignment = SetupMigrationCohortAssignment(organization);

        var subscription = CreateSubscription();
        SetupGetSubscription(organization, subscription);
        SetupActiveScheduleWithTwoPhases(subscription);

        var before = DateTime.UtcNow;

        var result = await _command.Run(organization);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sub_sched_123",
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.EndBehavior == SubscriptionScheduleEndBehavior.Release &&
                opts.Phases.Count == 2 &&
                opts.Phases[1].Discounts != null &&
                opts.Phases[1].Discounts.Any(d => d.Coupon == ChurnCouponCode)));

        await _assignmentRepository.Received(1).ReplaceAsync(
            Arg.Is<OrganizationPlanMigrationCohortAssignment>(a =>
                a.ChurnDiscountAppliedDate != null
                && a.ChurnDiscountAppliedDate >= before
                && a.ChurnDiscountAppliedDate <= DateTime.UtcNow.AddSeconds(1)));
    }

    [Fact]
    public async Task Run_MigrationCohort_PreservesPhase1AsIs()
    {
        var organization = CreateOrganization();
        SetupOfferEligible();
        SetupMigrationCohortAssignment(organization);

        var subscription = CreateSubscription();
        SetupGetSubscription(organization, subscription);

        var phase1Start = DateTime.UtcNow.AddDays(-90);
        var phase1End = DateTime.UtcNow.AddDays(180);
        var phase1Items = new List<SubscriptionSchedulePhaseItem>
        {
            new() { PriceId = "old-seat-price", Quantity = 10 }
        };
        var phase1Discounts = new List<SubscriptionSchedulePhaseDiscount>
        {
            new() { CouponId = "proactive-coupon" }
        };
        var phase1Metadata = new Dictionary<string, string> { ["migration_cohort_id"] = "cohort-A" };

        var schedule = new SubscriptionSchedule
        {
            Id = "sub_sched_123",
            SubscriptionId = subscription.Id,
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase
                {
                    StartDate = phase1Start,
                    EndDate = phase1End,
                    Items = phase1Items,
                    Discounts = phase1Discounts,
                    Metadata = phase1Metadata,
                    ProrationBehavior = ProrationBehavior.None
                },
                new SubscriptionSchedulePhase
                {
                    StartDate = phase1End,
                    EndDate = phase1End.AddYears(1),
                    Items = [new SubscriptionSchedulePhaseItem { PriceId = "new-seat-price", Quantity = 10 }],
                    Discounts = [],
                    ProrationBehavior = ProrationBehavior.None
                }
            ]
        };
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

        await _command.Run(organization);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sub_sched_123",
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases[0].StartDate == phase1Start &&
                opts.Phases[0].EndDate == phase1End &&
                opts.Phases[0].ProrationBehavior == ProrationBehavior.None &&
                opts.Phases[0].Items.Count == 1 &&
                opts.Phases[0].Items[0].Price == "old-seat-price" &&
                opts.Phases[0].Items[0].Quantity == 10 &&
                opts.Phases[0].Discounts != null &&
                opts.Phases[0].Discounts.Count == 1 &&
                opts.Phases[0].Discounts[0].Coupon == "proactive-coupon" &&
                opts.Phases[0].Metadata == phase1Metadata));
    }

    [Fact]
    public async Task Run_MigrationCohort_StacksOnExistingProactiveCouponOnPhase2_BothCouponsPresentInUpdate()
    {
        var organization = CreateOrganization();
        SetupOfferEligible();
        SetupMigrationCohortAssignment(organization);

        var subscription = CreateSubscription();
        SetupGetSubscription(organization, subscription);

        var phase1End = DateTime.UtcNow.AddDays(180);
        var schedule = new SubscriptionSchedule
        {
            Id = "sub_sched_123",
            SubscriptionId = subscription.Id,
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase
                {
                    StartDate = DateTime.UtcNow.AddDays(-90),
                    EndDate = phase1End,
                    Items = [],
                    ProrationBehavior = ProrationBehavior.None
                },
                new SubscriptionSchedulePhase
                {
                    StartDate = phase1End,
                    EndDate = phase1End.AddYears(1),
                    Items = [],
                    Discounts = [new SubscriptionSchedulePhaseDiscount { CouponId = "proactive-phase2-coupon" }],
                    ProrationBehavior = ProrationBehavior.None
                }
            ]
        };
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

        await _command.Run(organization);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sub_sched_123",
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases[1].Discounts != null &&
                opts.Phases[1].Discounts.Count == 2 &&
                opts.Phases[1].Discounts.Any(d => d.Coupon == "proactive-phase2-coupon") &&
                opts.Phases[1].Discounts.Any(d => d.Coupon == ChurnCouponCode)));
    }

    [Fact]
    public async Task Run_MigrationCohort_CustomerLevelDiscount_NotMirroredIntoPhase2Discounts()
    {
        var organization = CreateOrganization();
        SetupOfferEligible();
        SetupMigrationCohortAssignment(organization);

        var subscription = CreateSubscription(customerDiscount: new Discount
        {
            Coupon = new Coupon { Id = "customer-level-coupon" }
        });
        SetupGetSubscription(organization, subscription);
        SetupActiveScheduleWithTwoPhases(subscription);

        await _command.Run(organization);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sub_sched_123",
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases[1].Discounts != null &&
                opts.Phases[1].Discounts.All(d => d.Coupon != "customer-level-coupon")));
    }

    [Fact]
    public async Task Run_MigrationCohort_InvokedTwice_AppliesCouponOnce_AppliedDateNotOverwritten()
    {
        var organization = CreateOrganization();
        SetupOfferEligible();
        var assignment = SetupMigrationCohortAssignment(organization);

        var subscription = CreateSubscription();
        SetupGetSubscription(organization, subscription);

        var phase1End = DateTime.UtcNow.AddDays(180);
        var firstCallSchedule = new SubscriptionSchedule
        {
            Id = "sub_sched_123",
            SubscriptionId = subscription.Id,
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase
                {
                    StartDate = DateTime.UtcNow.AddDays(-90), EndDate = phase1End,
                    Items = [], ProrationBehavior = ProrationBehavior.None
                },
                new SubscriptionSchedulePhase
                {
                    StartDate = phase1End, EndDate = phase1End.AddYears(1),
                    Items = [], Discounts = [], ProrationBehavior = ProrationBehavior.None
                }
            ]
        };
        // Second call: coupon is already on Phase 2 (idempotency from a prior redeem).
        var secondCallSchedule = new SubscriptionSchedule
        {
            Id = "sub_sched_123",
            SubscriptionId = subscription.Id,
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase
                {
                    StartDate = DateTime.UtcNow.AddDays(-90), EndDate = phase1End,
                    Items = [], ProrationBehavior = ProrationBehavior.None
                },
                new SubscriptionSchedulePhase
                {
                    StartDate = phase1End, EndDate = phase1End.AddYears(1),
                    Items = [],
                    Discounts = [new SubscriptionSchedulePhaseDiscount { CouponId = ChurnCouponCode }],
                    ProrationBehavior = ProrationBehavior.None
                }
            ]
        };
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(
                new StripeList<SubscriptionSchedule> { Data = [firstCallSchedule] },
                new StripeList<SubscriptionSchedule> { Data = [secondCallSchedule] });

        await _command.Run(organization);
        var firstAppliedDate = assignment.ChurnDiscountAppliedDate;

        // Simulate the second call's revalidation: ChurnDiscountAppliedDate carries forward.
        _assignmentRepository.GetByOrganizationIdAsync(organization.Id).Returns(assignment);

        await _command.Run(organization);

        // Only one Stripe update; second call is a no-op at the Stripe layer.
        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>());
        Assert.Equal(firstAppliedDate, assignment.ChurnDiscountAppliedDate);
    }

    [Fact]
    public async Task Run_ChurnOnlyCohort_AppliesCoupon_WritesAppliedDate_WithinTolerance()
    {
        var organization = CreateOrganization();
        SetupOfferEligible();
        SetupChurnOnlyCohortAssignment(organization);

        var subscription = CreateSubscription();
        SetupGetSubscription(organization, subscription);

        var before = DateTime.UtcNow;

        var result = await _command.Run(organization);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.Discounts.Any(d => d.Coupon == ChurnCouponCode)));

        await _assignmentRepository.Received(1).ReplaceAsync(
            Arg.Is<OrganizationPlanMigrationCohortAssignment>(a =>
                a.ChurnDiscountAppliedDate != null
                && a.ChurnDiscountAppliedDate >= before
                && a.ChurnDiscountAppliedDate <= DateTime.UtcNow.AddSeconds(1)));
    }

    [Fact]
    public async Task Run_ChurnOnlyCohort_CustomerLevelDiscount_NotMirroredIntoSubscriptionDiscounts()
    {
        var organization = CreateOrganization();
        SetupOfferEligible();
        SetupChurnOnlyCohortAssignment(organization);

        var subscription = CreateSubscription(customerDiscount: new Discount
        {
            Coupon = new Coupon { Id = "customer-level-coupon" }
        });
        SetupGetSubscription(organization, subscription);

        await _command.Run(organization);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.Discounts.All(d => d.Coupon != "customer-level-coupon")));
    }

    [Fact]
    public async Task Run_MigrationCohort_StripeUpdateScheduleThrows_AppliedDateNotWritten_ExceptionBubbles()
    {
        var organization = CreateOrganization();
        SetupOfferEligible();
        SetupMigrationCohortAssignment(organization);

        var subscription = CreateSubscription();
        SetupGetSubscription(organization, subscription);
        SetupActiveScheduleWithTwoPhases(subscription);

        _stripeAdapter.UpdateSubscriptionScheduleAsync(Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>())
            .Returns<SubscriptionSchedule>(_ => throw new StripeException
            {
                StripeError = new StripeError { Code = "api_error", Message = "internal" }
            });

        var result = await _command.Run(organization);

        // Bubbles to BaseBillingCommand -> Unhandled.
        Assert.True(result.IsT3);
        await _assignmentRepository.DidNotReceive().ReplaceAsync(
            Arg.Any<OrganizationPlanMigrationCohortAssignment>());
    }

    [Fact]
    public async Task RedeemForChurnOnlyCohort_StripeUpdateThrows_RollsBackAppliedDate_OriginalExceptionBubbles()
    {
        var organization = CreateOrganization();
        SetupOfferEligible();
        SetupChurnOnlyCohortAssignment(organization);

        var subscription = CreateSubscription();
        SetupGetSubscription(organization, subscription);

        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns<Subscription>(_ => throw new StripeException
            {
                StripeError = new StripeError { Code = "api_error", Message = "internal" }
            });

        // Snapshot ChurnDiscountAppliedDate at each ReplaceAsync call. The command mutates the
        // same assignment instance for both the stamp and the rollback, so we can't compare
        // against the final state post-await -- capture at call time.
        var appliedDatesAtCallTime = new List<DateTime?>();
        _ = _assignmentRepository.ReplaceAsync(Arg.Do<OrganizationPlanMigrationCohortAssignment>(a =>
            appliedDatesAtCallTime.Add(a.ChurnDiscountAppliedDate)));

        var result = await _command.Run(organization);

        Assert.True(result.IsT3);

        // First call: stamp written (non-null). Second call: rollback (null).
        Assert.Equal(2, appliedDatesAtCallTime.Count);
        Assert.NotNull(appliedDatesAtCallTime[0]);
        Assert.Null(appliedDatesAtCallTime[1]);
    }

    [Fact]
    public async Task RedeemForChurnOnlyCohort_StripeFailsAndRollbackAlsoFails_FieldStaysSet_OriginalExceptionBubbles_RollbackErrorLogged()
    {
        var logger = Substitute.For<ILogger<RedeemChurnMitigationOfferCommand>>();
        var command = new RedeemChurnMitigationOfferCommand(
            logger, _getOfferQuery, _assignmentRepository, _cohortRepository, _stripeAdapter);

        var organization = CreateOrganization();
        SetupOfferEligible();
        var assignment = SetupChurnOnlyCohortAssignment(organization);

        var subscription = CreateSubscription();
        SetupGetSubscription(organization, subscription);

        var stripeException = new StripeException
        {
            StripeError = new StripeError { Code = "api_error", Message = "internal" }
        };
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns<Subscription>(_ => throw stripeException);

        // First ReplaceAsync (the stamp) succeeds; second ReplaceAsync (the rollback) throws.
        // Snapshot ChurnDiscountAppliedDate at call time -- the command mutates the same
        // assignment instance, so a post-await field check can't distinguish the two calls.
        var appliedDatesAtCallTime = new List<DateTime?>();
        var callCount = 0;
        var rollbackException = new InvalidOperationException("db down");
        _ = _assignmentRepository.ReplaceAsync(Arg.Do<OrganizationPlanMigrationCohortAssignment>(a =>
            {
                appliedDatesAtCallTime.Add(a.ChurnDiscountAppliedDate);
                callCount++;
                if (callCount == 2)
                {
                    throw rollbackException;
                }
            }));

        var result = await command.Run(organization);

        // BaseBillingCommand catches the original Stripe exception (not the rollback exception)
        // and converts it to Unhandled (IsT3).
        Assert.True(result.IsT3);

        // First call: stamp written (non-null). Second call: rollback attempt (null) -- but that
        // call threw, so the persisted ChurnDiscountAppliedDate remains the non-null stamp value
        // and ops must clear it manually.
        Assert.Equal(2, appliedDatesAtCallTime.Count);
        Assert.NotNull(appliedDatesAtCallTime[0]);
        Assert.Null(appliedDatesAtCallTime[1]);

        logger.ReceivedWithAnyArgs().Log<object>(LogLevel.Error, default, default!, default, default!);
    }

    [Fact]
    public async Task Run_MigrationCohort_ScheduleHasOnlyOnePhaseAfterFilter_ReturnsFailure()
    {
        var organization = CreateOrganization();
        SetupOfferEligible();
        SetupMigrationCohortAssignment(organization);

        var subscription = CreateSubscription();
        SetupGetSubscription(organization, subscription);

        var phase1End = DateTime.UtcNow.AddDays(180);
        var schedule = new SubscriptionSchedule
        {
            Id = "sub_sched_123",
            SubscriptionId = subscription.Id,
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                // Phase 1 still active
                new SubscriptionSchedulePhase
                {
                    StartDate = DateTime.UtcNow.AddDays(-90), EndDate = phase1End,
                    Items = [], ProrationBehavior = ProrationBehavior.None
                },
                // Phase 2 already ended in the past -- gets filtered out by EndDate > now.
                new SubscriptionSchedulePhase
                {
                    StartDate = DateTime.UtcNow.AddYears(-2),
                    EndDate = DateTime.UtcNow.AddDays(-1),
                    Items = [], ProrationBehavior = ProrationBehavior.None
                }
            ]
        };
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

        var result = await _command.Run(organization);

        Assert.True(result.IsT2);
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionScheduleAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>());
    }

    [Fact]
    public async Task Run_MigrationCohort_ScheduleHasThreeUnexpiredPhases_LogsWarning_ReturnsFailure()
    {
        var logger = Substitute.For<ILogger<RedeemChurnMitigationOfferCommand>>();
        var command = new RedeemChurnMitigationOfferCommand(
            logger, _getOfferQuery, _assignmentRepository, _cohortRepository, _stripeAdapter);

        var organization = CreateOrganization();
        SetupOfferEligible();
        SetupMigrationCohortAssignment(organization);

        var subscription = CreateSubscription();
        SetupGetSubscription(organization, subscription);

        var phase1End = DateTime.UtcNow.AddDays(180);
        var phase2End = phase1End.AddYears(1);
        var schedule = new SubscriptionSchedule
        {
            Id = "sub_sched_123",
            SubscriptionId = subscription.Id,
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase
                {
                    StartDate = DateTime.UtcNow.AddDays(-90), EndDate = phase1End,
                    Items = [], ProrationBehavior = ProrationBehavior.None
                },
                new SubscriptionSchedulePhase
                {
                    StartDate = phase1End, EndDate = phase2End,
                    Items = [], ProrationBehavior = ProrationBehavior.None
                },
                new SubscriptionSchedulePhase
                {
                    StartDate = phase2End, EndDate = phase2End.AddYears(1),
                    Items = [], ProrationBehavior = ProrationBehavior.None
                }
            ]
        };
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

        var result = await command.Run(organization);

        Assert.True(result.IsT2);
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionScheduleAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>());
        logger.ReceivedWithAnyArgs().Log<object>(LogLevel.Warning, default, default!, default, default!);
    }

    // ---- helpers ----

    private static Organization CreateOrganization() => new()
    {
        Id = Guid.NewGuid(),
        GatewaySubscriptionId = "sub_123"
    };

    private static Subscription CreateSubscription(Discount? customerDiscount = null) =>
        new()
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Customer = new Customer { Id = "cus_123", Discount = customerDiscount },
            Items = new StripeList<SubscriptionItem> { Data = [] }
        };

    private void SetupGetSubscription(Organization organization, Subscription subscription)
    {
        _stripeAdapter
            .GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
    }

    private void SetupOfferEligible() =>
        _getOfferQuery.Run(Arg.Any<Organization>()).Returns(new ChurnMitigationOfferResult(
            CouponId: ChurnCouponCode,
            PercentOff: 15m,
            AmountOff: null,
            Duration: "once",
            DurationInMonths: null,
            Name: "Churn 15% off"));

    private OrganizationPlanMigrationCohortAssignment SetupMigrationCohortAssignment(Organization organization)
    {
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            CohortId = cohortId
        };
        _assignmentRepository.GetByOrganizationIdAsync(organization.Id).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "migration-cohort",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            ChurnDiscountCouponCode = ChurnCouponCode,
            IsActive = true
        });
        return assignment;
    }

    private OrganizationPlanMigrationCohortAssignment SetupChurnOnlyCohortAssignment(Organization organization)
    {
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            CohortId = cohortId
        };
        _assignmentRepository.GetByOrganizationIdAsync(organization.Id).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "churn-only-cohort",
            MigrationPathId = null,
            ChurnDiscountCouponCode = ChurnCouponCode,
            IsActive = true
        });
        return assignment;
    }

    private void SetupActiveScheduleWithTwoPhases(Subscription subscription)
    {
        var phase1End = DateTime.UtcNow.AddDays(180);
        var schedule = new SubscriptionSchedule
        {
            Id = "sub_sched_123",
            SubscriptionId = subscription.Id,
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase
                {
                    StartDate = DateTime.UtcNow.AddDays(-90), EndDate = phase1End,
                    Items = [], ProrationBehavior = ProrationBehavior.None
                },
                new SubscriptionSchedulePhase
                {
                    StartDate = phase1End, EndDate = phase1End.AddYears(1),
                    Items = [], Discounts = [], ProrationBehavior = ProrationBehavior.None
                }
            ]
        };
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });
    }
}
