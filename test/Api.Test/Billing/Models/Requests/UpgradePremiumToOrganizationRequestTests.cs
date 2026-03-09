using Bit.Api.Billing.Models.Requests.Payment;
using Bit.Api.Billing.Models.Requests.Premium;
using Bit.Core.Billing.Enums;
using Xunit;

namespace Bit.Api.Test.Billing.Models.Requests;

public class UpgradePremiumToOrganizationRequestTests
{
    [Theory]
    [InlineData(ProductTierType.Families, PlanType.FamiliesAnnually)]
    [InlineData(ProductTierType.Teams, PlanType.TeamsAnnually)]
    [InlineData(ProductTierType.Enterprise, PlanType.EnterpriseAnnually)]
    public void ToDomain_ValidTierTypes_ReturnsPlanType(ProductTierType tierType, PlanType expectedPlanType)
    {
        // Arrange
        var sut = new UpgradePremiumToOrganizationRequest
        {
            OrganizationName = "Test Organization",
            Key = "encrypted-key",
            PublicKey = "public-key",
            EncryptedPrivateKey = "encrypted-private-key",
            CollectionName = "Default Collection",
            TargetProductTierType = tierType,
            BillingAddress = new MinimalBillingAddressRequest
            {
                Country = "US",
                PostalCode = "12345"
            }
        };

        // Act
        var (organizationName, key, publicKey, encryptedPrivateKey, collectionName, planType, billingAddress) = sut.ToDomain();

        // Assert
        Assert.Equal("Test Organization", organizationName);
        Assert.Equal("encrypted-key", key);
        Assert.Equal("public-key", publicKey);
        Assert.Equal("encrypted-private-key", encryptedPrivateKey);
        Assert.Equal("Default Collection", collectionName);
        Assert.Equal(expectedPlanType, planType);
        Assert.Equal("US", billingAddress.Country);
        Assert.Equal("12345", billingAddress.PostalCode);
    }

    [Theory]
    [InlineData(ProductTierType.Free)]
    [InlineData(ProductTierType.TeamsStarter)]
    public void ToDomain_InvalidTierTypes_ThrowsInvalidOperationException(ProductTierType tierType)
    {
        // Arrange
        var sut = new UpgradePremiumToOrganizationRequest
        {
            OrganizationName = "Test Organization",
            Key = "encrypted-key",
            PublicKey = "public-key",
            EncryptedPrivateKey = "encrypted-private-key",
            TargetProductTierType = tierType,
            BillingAddress = new MinimalBillingAddressRequest
            {
                Country = "US",
                PostalCode = "12345"
            }
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => sut.ToDomain());
        Assert.Contains($"Cannot upgrade Premium subscription to {tierType} plan", exception.Message);
    }
}
