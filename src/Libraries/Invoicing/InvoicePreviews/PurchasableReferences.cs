using System.Diagnostics.CodeAnalysis;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;

namespace Bit.Invoicing.InvoicePreviews;

/// <summary>Maps purchasable references to their product, and tells whether a reference is known.</summary>
internal static class PurchasableReferences
{
    private static readonly IReadOnlyDictionary<string, ProductType> ProductsByReference = new Dictionary<string, ProductType>
    {
        [StripeConstants.PurchasableReferences.PasswordManagerSeat] = ProductType.PasswordManager,
        [StripeConstants.PurchasableReferences.PasswordManagerStorage] = ProductType.PasswordManager,
        [StripeConstants.PurchasableReferences.SecretsManagerSeat] = ProductType.SecretsManager,
        [StripeConstants.PurchasableReferences.SecretsManagerServiceAccount] = ProductType.SecretsManager,
    };

    /// <summary>True when the reference maps to a known product. Tolerates null/empty.</summary>
    internal static bool IsKnown([NotNullWhen(true)] string? reference) =>
        reference is not null && ProductsByReference.ContainsKey(reference);

    /// <summary>The product a reference belongs to, or null when the reference is unknown.</summary>
    internal static ProductType? ProductOf(string reference) =>
        ProductsByReference.TryGetValue(reference, out var product) ? product : null;
}
