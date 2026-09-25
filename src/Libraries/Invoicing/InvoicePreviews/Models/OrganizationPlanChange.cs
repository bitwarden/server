using Bit.Core.Billing.Enums;

namespace Bit.Invoicing.InvoicePreviews.Models;

/// <summary>
/// The target of an organization plan change plus the address to tax it at.
/// </summary>
public record OrganizationPlanChange
{
    public required PlanTierType Tier { get; init; }
    public required PlanCadenceType Cadence { get; init; }
    public required string Country { get; init; }
    public required string PostalCode { get; init; }
}
