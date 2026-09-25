using Bit.Core.Billing.Enums;

namespace Bit.Invoicing.InvoicePreviews.Models;

/// <summary>
/// The target of an organization plan change plus the address to tax it at. Resolves to the concrete
/// <see cref="PlanType"/> the pricing client and Stripe prices are keyed on.
/// </summary>
public record OrganizationPlanChange
{
    public required PlanTierType Tier { get; init; }
    public required PlanCadenceType Cadence { get; init; }
    public required string Country { get; init; }
    public required string PostalCode { get; init; }

    public PlanType PlanType => Tier switch
    {
        PlanTierType.Families => PlanType.FamiliesAnnually,
        PlanTierType.Teams => Cadence == PlanCadenceType.Monthly
            ? PlanType.TeamsMonthly
            : PlanType.TeamsAnnually,
        PlanTierType.Enterprise => Cadence == PlanCadenceType.Monthly
            ? PlanType.EnterpriseMonthly
            : PlanType.EnterpriseAnnually,
        _ => throw new InvalidOperationException($"Cannot change an organization to the {Tier} tier.")
    };
}
