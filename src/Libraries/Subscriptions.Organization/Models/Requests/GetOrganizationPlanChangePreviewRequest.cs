using Bit.Core.Billing.Enums;
using Bit.Invoicing.InvoicePreviews.Models;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Subscriptions.Organization.Models.Requests;

internal record GetOrganizationPlanChangePreviewRequest
{
    [FromQuery(Name = "tier")]
    public required EnumMemberParameter<PlanTierType> Tier { get; init; }

    [FromQuery(Name = "cadence")]
    public required EnumMemberParameter<PlanCadenceType> Cadence { get; init; }

    [FromQuery(Name = "country")]
    public required string Country { get; init; }

    [FromQuery(Name = "postalCode")]
    public required string PostalCode { get; init; }

    public OrganizationPlanChange ToDomain() => new()
    {
        Tier = Tier.Value,
        Cadence = Cadence.Value,
        Country = Country,
        PostalCode = PostalCode
    };
}
