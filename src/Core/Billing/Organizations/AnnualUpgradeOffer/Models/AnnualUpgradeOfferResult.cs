namespace Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Models;

public sealed record AnnualUpgradeOfferResult(
    decimal CurrentAnnualCost,
    decimal NewAnnualCost,
    decimal Savings);
