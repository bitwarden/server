using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Organizations.Schedules.Enums;
using Bit.Core.Billing.Organizations.Schedules.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Test.Billing.Mocks.Plans;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.Schedules.Queries;

using static StripeConstants;

public class GetOrganizationSubscriptionScheduleOwnershipQueryTests
{
    private readonly IOrganizationPlanMigrationCohortAssignmentRepository _assignmentRepository =
        Substitute.For<IOrganizationPlanMigrationCohortAssignmentRepository>();
    private readonly IOrganizationPlanMigrationCohortRepository _cohortRepository =
        Substitute.For<IOrganizationPlanMigrationCohortRepository>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly ILogger<GetOrganizationSubscriptionScheduleOwnershipQuery> _logger =
        Substitute.For<ILogger<GetOrganizationSubscriptionScheduleOwnershipQuery>>();
    private readonly GetOrganizationSubscriptionScheduleOwnershipQuery _query;

    private readonly Teams2020Plan _currentPlan = new(isAnnual: false);
    private readonly TeamsPlan _annualLatestPlan = new(isAnnual: true);

    public GetOrganizationSubscriptionScheduleOwnershipQueryTests()
    {
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(_annualLatestPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(_currentPlan);
        _query = new GetOrganizationSubscriptionScheduleOwnershipQuery(
            _logger, _assignmentRepository, _cohortRepository, _pricingClient);
    }

    private static Organization CreateOrganization(PlanType planType = PlanType.TeamsMonthly2020) => new()
    {
        Id = Guid.NewGuid(),
        PlanType = planType,
        GatewaySubscriptionId = "sub_123"
    };

    private static Subscription CreateSubscription(SubscriptionSchedule? schedule, string? scheduleId = null) =>
        new()
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Schedule = schedule,
            ScheduleId = scheduleId ?? schedule?.Id
        };

    private static SubscriptionSchedule CreateSchedule(string status, params string[] priceIds) => new()
    {
        Id = "sub_sched_123",
        Status = status,
        Phases =
        [
            new SubscriptionSchedulePhase
            {
                Items = [.. priceIds.Select(id => new SubscriptionSchedulePhaseItem { PriceId = id })]
            }
        ]
    };

    private void GiveOrganizationAMigrationCohort(Organization organization, MigrationPathId? migrationPathId)
    {
        var cohortId = Guid.NewGuid();
        _assignmentRepository.GetByOrganizationIdAsync(organization.Id).Returns(
            new OrganizationPlanMigrationCohortAssignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = organization.Id,
                CohortId = cohortId
            });
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "Cohort A1",
            MigrationPathId = migrationPathId
        });
    }

    [Fact]
    public async Task Run_NoAttachedSchedule_ReturnsNone()
    {
        var organization = CreateOrganization();

        var result = await _query.Run(organization, CreateSubscription(schedule: null, scheduleId: null));

        Assert.Equal(OrganizationSubscriptionScheduleOwnership.None, result.Ownership);
        Assert.Null(result.Schedule);
    }

    [Fact]
    public async Task Run_ScheduleNotActive_ReturnsNone()
    {
        var organization = CreateOrganization();
        var schedule = CreateSchedule(SubscriptionScheduleStatus.Released,
            _annualLatestPlan.PasswordManager.StripeSeatPlanId);

        var result = await _query.Run(organization, CreateSubscription(schedule));

        Assert.Equal(OrganizationSubscriptionScheduleOwnership.None, result.Ownership);
    }

    [Fact]
    public async Task Run_ScheduleIdSetButNotExpanded_ReturnsForeign()
    {
        var organization = CreateOrganization();

        var result = await _query.Run(
            organization, CreateSubscription(schedule: null, scheduleId: "sub_sched_123"));

        Assert.Equal(OrganizationSubscriptionScheduleOwnership.Foreign, result.Ownership);
        Assert.Null(result.Schedule);
    }

    [Fact]
    public async Task Run_PhaseCarriesAnnualLatestSeatPrice_ReturnsAnnualUpgrade()
    {
        var organization = CreateOrganization();
        var schedule = CreateSchedule(SubscriptionScheduleStatus.Active,
            _annualLatestPlan.PasswordManager.StripeSeatPlanId);

        var result = await _query.Run(organization, CreateSubscription(schedule));

        Assert.Equal(OrganizationSubscriptionScheduleOwnership.AnnualUpgrade, result.Ownership);
        Assert.Same(schedule, result.Schedule);
        await _assignmentRepository.DidNotReceiveWithAnyArgs().GetByOrganizationIdAsync(default);
    }

    [Fact]
    public async Task Run_NoAnnualLatestMapping_FallsThroughToMigrationCheck()
    {
        var organization = CreateOrganization(PlanType.TeamsAnnually);
        GiveOrganizationAMigrationCohort(organization, MigrationPathId.Teams2020MonthlyToCurrent);
        var schedule = CreateSchedule(SubscriptionScheduleStatus.Active, "price_anything");

        var result = await _query.Run(organization, CreateSubscription(schedule));

        Assert.Equal(OrganizationSubscriptionScheduleOwnership.PriceMigration, result.Ownership);
    }

    [Fact]
    public async Task Run_MigrationCohortAssignment_ReturnsPriceMigration()
    {
        var organization = CreateOrganization();
        GiveOrganizationAMigrationCohort(organization, MigrationPathId.Teams2020MonthlyToCurrent);
        var schedule = CreateSchedule(SubscriptionScheduleStatus.Active,
            _currentPlan.PasswordManager.StripeSeatPlanId);

        var result = await _query.Run(organization, CreateSubscription(schedule));

        Assert.Equal(OrganizationSubscriptionScheduleOwnership.PriceMigration, result.Ownership);
        Assert.Same(schedule, result.Schedule);
    }

    [Fact]
    public async Task Run_CohortWithNullMigrationPathId_ReturnsForeign()
    {
        var organization = CreateOrganization();
        GiveOrganizationAMigrationCohort(organization, migrationPathId: null);
        var schedule = CreateSchedule(SubscriptionScheduleStatus.Active, "price_negotiated");

        var result = await _query.Run(organization, CreateSubscription(schedule));

        Assert.Equal(OrganizationSubscriptionScheduleOwnership.Foreign, result.Ownership);
    }

    [Fact]
    public async Task Run_NoCohortAssignment_ReturnsForeign()
    {
        var organization = CreateOrganization();
        _assignmentRepository.GetByOrganizationIdAsync(organization.Id)
            .Returns((OrganizationPlanMigrationCohortAssignment?)null);
        var schedule = CreateSchedule(SubscriptionScheduleStatus.Active, "price_negotiated");

        var result = await _query.Run(organization, CreateSubscription(schedule));

        Assert.Equal(OrganizationSubscriptionScheduleOwnership.Foreign, result.Ownership);
        Assert.Same(schedule, result.Schedule);
    }
}
