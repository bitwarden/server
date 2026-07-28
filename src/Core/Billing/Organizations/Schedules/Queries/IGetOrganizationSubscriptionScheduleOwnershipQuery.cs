using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.Schedules.Models;
using Stripe;

namespace Bit.Core.Billing.Organizations.Schedules.Queries;

/// <summary>
/// Classifies the Stripe subscription schedule attached to an organization's subscription.
/// </summary>
/// <remarks>
/// Caller contract: <c>subscription</c> must be loaded with <c>schedule</c> expanded. The query
/// makes no Stripe calls of its own. When the subscription reports a schedule ID but the schedule
/// was not expanded, the query fails closed and reports
/// <see cref="Enums.OrganizationSubscriptionScheduleOwnership.Foreign"/> rather than pretending
/// nothing is attached.
/// </remarks>
public interface IGetOrganizationSubscriptionScheduleOwnershipQuery
{
    Task<OrganizationSubscriptionScheduleOwnershipResult> Run(Organization organization, Subscription subscription);
}
