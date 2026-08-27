using Bit.Core.Billing.Constants;
using Xunit;

namespace Bit.Invoicing.Test;

public class StripeConstantsPurchasableReferenceTests
{
    [Fact] public void MetadataKey_IsPurchasableReference() => Assert.Equal("purchasable_reference", StripeConstants.MetadataKeys.PurchasableReference);
    [Fact] public void PasswordManagerSeat_IsPmSeat() => Assert.Equal("pm-seat", StripeConstants.PurchasableReferences.PasswordManagerSeat);
    [Fact] public void PasswordManagerStorage_IsPmStorage() => Assert.Equal("pm-storage", StripeConstants.PurchasableReferences.PasswordManagerStorage);
    [Fact] public void SecretsManagerSeat_IsSmSeat() => Assert.Equal("sm-seat", StripeConstants.PurchasableReferences.SecretsManagerSeat);
    [Fact] public void SecretsManagerServiceAccount_IsSmServiceAccount() => Assert.Equal("sm-service-account", StripeConstants.PurchasableReferences.SecretsManagerServiceAccount);
}
