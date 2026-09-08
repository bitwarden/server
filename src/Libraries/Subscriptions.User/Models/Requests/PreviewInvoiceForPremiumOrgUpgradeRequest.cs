using System.Text.Json.Serialization;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Exceptions;

namespace Bit.Subscriptions.User.Models.Requests;

/// <summary>
/// Mirrors the legacy <c>PreviewPremiumUpgradeProrationRequest</c> JSON. Minimal APIs do not run DataAnnotations,
/// so validation lives in <see cref="ToDomain"/>.
/// </summary>
public record PreviewInvoiceForPremiumOrgUpgradeRequest
{
    // The web client sends the numeric enum value; the string converter accepts integers by default.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required ProductTierType TargetProductTierType { get; init; }

    public required PreviewInvoiceBillingAddressRequest BillingAddress { get; init; }

    /// <summary>The upgrade is annual-only; any other tier is caller error.</summary>
    private PlanType PlanType => TargetProductTierType switch
    {
        ProductTierType.Families => PlanType.FamiliesAnnually,
        ProductTierType.Teams => PlanType.TeamsAnnually,
        ProductTierType.Enterprise => PlanType.EnterpriseAnnually,
        _ => throw new BadRequestException(
            nameof(TargetProductTierType),
            $"Cannot upgrade Premium subscription to {TargetProductTierType} plan.")
    };

    public (PlanType PlanType, BillingAddress BillingAddress) ToDomain() =>
        (PlanType, BillingAddress.ToDomain());
}

/// <summary>Billing address for tax calculation. Declared here because this library does not reference <c>Api</c>.</summary>
public record PreviewInvoiceBillingAddressRequest
{
    public required string Country { get; init; }

    public required string PostalCode { get; init; }

    public BillingAddress ToDomain()
    {
        if (string.IsNullOrWhiteSpace(Country) || Country.Length != 2)
        {
            throw new BadRequestException(nameof(Country), "Country code must be 2 characters long.");
        }

        if (string.IsNullOrWhiteSpace(PostalCode))
        {
            throw new BadRequestException(nameof(PostalCode), "The PostalCode field is required.");
        }

        return new BillingAddress { Country = Country, PostalCode = PostalCode };
    }
}
