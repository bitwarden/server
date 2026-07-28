using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;
using Bit.Core.Billing.Organizations.Schedules.Enums;
using Bit.Core.Billing.Organizations.Schedules.Models;
using Bit.Core.Billing.Pricing;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Billing.Organizations.Schedules.Queries;

using static StripeConstants;

public class GetOrganizationSubscriptionScheduleOwnershipQuery(
    ILogger<GetOrganizationSubscriptionScheduleOwnershipQuery> logger,
    IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository,
    IOrganizationPlanMigrationCohortRepository cohortRepository,
    IPricingClient pricingClient) : IGetOrganizationSubscriptionScheduleOwnershipQuery
{
    private const string QueryName = nameof(GetOrganizationSubscriptionScheduleOwnershipQuery);

    public async Task<OrganizationSubscriptionScheduleOwnershipResult> Run(
        Organization organization, Subscription subscription)
    {
        if (string.IsNullOrEmpty(subscription.ScheduleId))
        {
            return new OrganizationSubscriptionScheduleOwnershipResult(
                OrganizationSubscriptionScheduleOwnership.None, null);
        }

        var schedule = subscription.Schedule;
        if (schedule is null)
        {
            // Fail closed. Reporting None here would let a caller release a schedule it never saw.
            logger.LogError(
                "{Query}: Subscription ({SubscriptionId}) for Organization ({OrganizationId}) reports schedule ({ScheduleId}) but it was not expanded; treating as foreign",
                QueryName, subscription.Id, organization.Id, subscription.ScheduleId);
            return new OrganizationSubscriptionScheduleOwnershipResult(
                OrganizationSubscriptionScheduleOwnership.Foreign, null);
        }

        if (schedule.Status != SubscriptionScheduleStatus.Active)
        {
            return new OrganizationSubscriptionScheduleOwnershipResult(
                OrganizationSubscriptionScheduleOwnership.None, null);
        }

        // Cheapest first. An annual-upgrade schedule has no cohort assignment because redemption
        // deletes it, so it is recognised by its contents. Only meaningful for plans the offer can
        // upgrade from, hence the mapping check.
        var annualLatestPlanType = AnnualUpgradeOfferPlans.ResolveAnnualLatestPlanType(organization.PlanType);
        if (annualLatestPlanType is not null)
        {
            var annualLatestPlan = await pricingClient.GetPlanOrThrow(annualLatestPlanType.Value);
            var carriesAnnualLatestSeatPrice = schedule.Phases?.Any(phase =>
                phase.Items.Any(item => item.PriceId == annualLatestPlan.PasswordManager.StripeSeatPlanId)) ?? false;

            if (carriesAnnualLatestSeatPrice)
            {
                return new OrganizationSubscriptionScheduleOwnershipResult(
                    OrganizationSubscriptionScheduleOwnership.AnnualUpgrade, schedule);
            }
        }

        // Matches the discriminator PM-40537 introduced in UpdateOrganizationSubscriptionCommand.
        // Known gap, inherited deliberately: assignment rows are never cleared after a migration
        // completes, so an already-migrated organization that later receives a hand-built schedule
        // is classified as ours. Preserving phase metadata is the fix, tracked separately.
        var assignment = await assignmentRepository.GetByOrganizationIdAsync(organization.Id);
        if (assignment is not null)
        {
            var cohort = await cohortRepository.GetByIdAsync(assignment.CohortId);
            if (cohort?.MigrationPathId is not null &&
                MigrationPaths.FromId(cohort.MigrationPathId.Value) is not null)
            {
                return new OrganizationSubscriptionScheduleOwnershipResult(
                    OrganizationSubscriptionScheduleOwnership.PriceMigration, schedule);
            }
        }

        return new OrganizationSubscriptionScheduleOwnershipResult(
            OrganizationSubscriptionScheduleOwnership.Foreign, schedule);
    }
}
