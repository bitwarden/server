using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.Billing.Pricing.Enums;
using Bit.Core.Models.Business;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models;

public class PaymentsSubscription
{
    public ProductTierType ProductTierType { get; init; }
    public string SubscriptionStatus { get; init; }

    public PaymentsSubscription() { }

    public PaymentsSubscription(SubscriptionInfo subscriptionInfo, InviteOrganization inviteOrganization)
    {
        SubscriptionStatus = subscriptionInfo?.Subscription?.Status ?? string.Empty;
        ProductTierType = inviteOrganization.Plan.ProductTier;
    }
}
