using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Invoicing.InvoicePreviews;
using Xunit;

namespace Bit.Invoicing.Test;

public class PurchasableReferencesTests
{
    [Theory]
    [InlineData(StripeConstants.PurchasableReferences.PasswordManagerSeat)]
    [InlineData(StripeConstants.PurchasableReferences.PasswordManagerStorage)]
    [InlineData(StripeConstants.PurchasableReferences.SecretsManagerSeat)]
    [InlineData(StripeConstants.PurchasableReferences.SecretsManagerServiceAccount)]
    public void IsKnown_TrueForKnownReference(string reference)
        => Assert.True(PurchasableReferences.IsKnown(reference));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("provider-seat")]
    [InlineData("PM-SEAT")]
    public void IsKnown_FalseForUnknownReference(string? reference)
        => Assert.False(PurchasableReferences.IsKnown(reference));

    [Theory]
    [InlineData(StripeConstants.PurchasableReferences.PasswordManagerSeat, ProductType.PasswordManager)]
    [InlineData(StripeConstants.PurchasableReferences.PasswordManagerStorage, ProductType.PasswordManager)]
    [InlineData(StripeConstants.PurchasableReferences.SecretsManagerSeat, ProductType.SecretsManager)]
    [InlineData(StripeConstants.PurchasableReferences.SecretsManagerServiceAccount, ProductType.SecretsManager)]
    public void ProductOf_MapsReferenceToProduct(string reference, ProductType expected)
        => Assert.Equal(expected, PurchasableReferences.ProductOf(reference));

    [Fact]
    public void ProductOf_UnknownReference_ReturnsNull()
        => Assert.Null(PurchasableReferences.ProductOf("fake-reference"));
}
