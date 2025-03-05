using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.Billing.Enums;
using Bit.Core.Models.Business;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models;

public class PaymentSubscriptionDto
{
    public ProductTierType ProductTierType { get; init; }
    public string SubscriptionStatus { get; init; }

    public static PaymentSubscriptionDto FromSubscriptionInfo(SubscriptionInfo subscriptionInfo, InviteOrganization inviteOrganization) =>
        new()
        {
            SubscriptionStatus = subscriptionInfo?.Subscription?.Status ?? string.Empty,
            ProductTierType = inviteOrganization.Plan.ProductTier
        };
}
