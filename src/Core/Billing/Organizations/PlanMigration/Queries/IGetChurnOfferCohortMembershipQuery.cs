using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Models;

namespace Bit.Core.Billing.Organizations.PlanMigration.Queries;

/// <summary>
/// Determines whether an organization is a member of a cohort eligible for the churn-mitigation
/// coupon offer -- the DB pre-filter shared by <see cref="GetChurnMitigationOfferQuery"/> and any
/// feature that must exclude churn-offer-eligible orgs, independent of whether that offer is
/// currently live (e.g. its one-shot coupon may already be consumed).
/// </summary>
public interface IGetChurnOfferCohortMembershipQuery
{
    Task<ChurnOfferCohortMembership?> Run(Organization organization);
}
