using Bit.Core.Billing.Enums;

namespace Bit.Subscriptions.User.Models.Requests;

internal record GetSubscriptionUpgradePreviewRequest(
    ProductTierType TargetProductTierType,
    string? Country,
    string? PostalCode);
