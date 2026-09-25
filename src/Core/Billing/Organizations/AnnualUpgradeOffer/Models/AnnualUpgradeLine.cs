using Stripe;

namespace Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Models;

/// <summary>A subscription line item paired with the annual price that replaces it.</summary>
internal readonly record struct AnnualUpgradeLine(SubscriptionItem Item, string TargetPriceId);
