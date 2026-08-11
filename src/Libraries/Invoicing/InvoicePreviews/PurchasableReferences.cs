using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;

namespace Bit.Invoicing.InvoicePreviews;

internal static class PurchasableReferences
{
    private static readonly IReadOnlyDictionary<string, ProductType> ProductsByReference = new Dictionary<string, ProductType>
    {
        [StripeConstants.PurchasableReferences.PasswordManagerSeat] = ProductType.PasswordManager,
        [StripeConstants.PurchasableReferences.PasswordManagerStorage] = ProductType.PasswordManager,
        [StripeConstants.PurchasableReferences.SecretsManagerSeat] = ProductType.SecretsManager,
        [StripeConstants.PurchasableReferences.SecretsManagerServiceAccount] = ProductType.SecretsManager,
    };

    internal static bool IsKnown(string reference) => ProductsByReference.ContainsKey(reference);

    internal static ProductType ProductOf(string reference) => ProductsByReference[reference];
}
