using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Subscriptions;

namespace Bit.Core.Billing.Premium.Models;

public record PremiumToOrganizationUpgrade
{
    /// <summary>
    /// The name for the new organization.
    /// </summary>
    public required string OrganizationName { get; init; }

    /// <summary>
    /// The encrypted organization key for the owner.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// The organization's public key.
    /// </summary>
    public required string PublicKey { get; init; }

    /// <summary>
    /// The organization's encrypted private key.
    /// </summary>
    public required string EncryptedPrivateKey { get; init; }

    /// <summary>
    /// Optional name for the default collection.
    /// </summary>
    public string? CollectionName { get; init; }

    /// <summary>
    /// The target organization plan type to upgrade to.
    /// </summary>
    public required PlanType TargetPlanType { get; init; }

    /// <summary>
    /// The billing address for tax calculation.
    /// </summary>
    public required BillingAddress BillingAddress { get; init; }

    /// <summary>
    /// The time the client's invoice preview was pinned to. When provided, it is validated before any change is made
    /// (throwing <see cref="InvoicePreviewExpiredException"/> if it is stale) and used as the proration time.
    /// </summary>
    public DateTime? ProrationDate { get; init; }
}
