using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Billing.Organizations.Queries;

using static StripeConstants;

public interface IGetOrganizationTrialQuery
{
    /// <summary>
    /// Returns the trial on the organization's Stripe subscription, or <see langword="null"/> when the organization
    /// has no subscription or the subscription is not trialing. Stripe transport failures propagate to the caller.
    /// </summary>
    Task<OrganizationTrial?> Run(Organization organization);
}

public class GetOrganizationTrialQuery(
    IStripeAdapter stripeAdapter,
    ILogger<GetOrganizationTrialQuery> logger) : IGetOrganizationTrialQuery
{
    public async Task<OrganizationTrial?> Run(Organization organization)
    {
        if (string.IsNullOrEmpty(organization.GatewaySubscriptionId))
        {
            return null;
        }

        var subscription = await OrganizationSubscriptionHelpers.TryGetSubscriptionAsync(
            stripeAdapter, logger, organization, ["test_clock"]);

        if (subscription is not { Status: SubscriptionStatus.Trialing, TrialEnd: { } trialEnd })
        {
            return null;
        }

        return new OrganizationTrial(trialEnd, TrialExtensionPolicy.GetIneligibilityReason(subscription));
    }
}
