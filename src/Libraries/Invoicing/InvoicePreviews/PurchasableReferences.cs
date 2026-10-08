using System.Diagnostics.CodeAnalysis;
using Bit.Core.Billing.Constants;

namespace Bit.Invoicing.InvoicePreviews;

/// <summary>Maps purchasable references to the preview section they render under, and tells whether a reference is known.</summary>
internal static class PurchasableReferences
{
    private static readonly IReadOnlyDictionary<string, InvoicePreviewSection> SectionsByReference = new Dictionary<string, InvoicePreviewSection>
    {
        [StripeConstants.PurchasableReferences.PasswordManagerSeat] = InvoicePreviewSection.PasswordManager,
        [StripeConstants.PurchasableReferences.PasswordManagerStorage] = InvoicePreviewSection.PasswordManager,
        [StripeConstants.PurchasableReferences.SecretsManagerSeat] = InvoicePreviewSection.SecretsManager,
        [StripeConstants.PurchasableReferences.SecretsManagerServiceAccount] = InvoicePreviewSection.SecretsManager,
        [StripeConstants.PurchasableReferences.PrivilegedControlsSeat] = InvoicePreviewSection.PrivilegedControls,
    };

    /// <summary>True when the reference is in the table. Tolerates null/empty.</summary>
    internal static bool IsKnown([NotNullWhen(true)] string? reference) =>
        reference is not null && SectionsByReference.ContainsKey(reference);

    /// <summary>The section a reference renders under, or null when the reference is unknown.</summary>
    internal static InvoicePreviewSection? SectionOf(string reference) =>
        SectionsByReference.TryGetValue(reference, out var section) ? section : null;
}
