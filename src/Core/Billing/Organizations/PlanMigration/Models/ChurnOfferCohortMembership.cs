using Bit.Core.Billing.Organizations.PlanMigration.Entities;

namespace Bit.Core.Billing.Organizations.PlanMigration.Models;

public sealed record ChurnOfferCohortMembership(
    OrganizationPlanMigrationCohortAssignment Assignment,
    OrganizationPlanMigrationCohort Cohort);
