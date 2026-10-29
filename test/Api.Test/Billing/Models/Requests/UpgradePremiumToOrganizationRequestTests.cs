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
            BillingAddress = new CheckoutBillingAddressRequest
            {
                Country = "US",
                PostalCode = "12345"
            },
            ProrationDate = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var upgrade = sut.ToDomain();

        // Assert
        Assert.Equal("Test Organization", upgrade.OrganizationName);
        Assert.Equal("encrypted-key", upgrade.Key);
        Assert.Equal("public-key", upgrade.PublicKey);
        Assert.Equal("encrypted-private-key", upgrade.EncryptedPrivateKey);
        Assert.Equal("Default Collection", upgrade.CollectionName);
        Assert.Equal(expectedPlanType, upgrade.TargetPlanType);
        Assert.Equal("US", upgrade.BillingAddress.Country);
        Assert.Equal("12345", upgrade.BillingAddress.PostalCode);
        Assert.Equal(new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc), upgrade.ProrationDate);
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
            BillingAddress = new CheckoutBillingAddressRequest
            {
                Country = "US",
                PostalCode = "12345"
            }
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => sut.ToDomain());
        Assert.Contains($"Cannot upgrade Premium subscription to {tierType} plan", exception.Message);
    }

    [Theory]
    [InlineData(ProductTierType.Teams, PlanType.TeamsAnnually, "DE", "10115", "eu_vat", "DE123456789")]
    [InlineData(ProductTierType.Enterprise, PlanType.EnterpriseAnnually, "FR", "75001", "eu_vat", "FR12345678901")]
    public void ToDomain_BusinessPlansWithNonUsTaxId_IncludesTaxIdInBillingAddress(
        ProductTierType tierType,
        PlanType expectedPlanType,
        string country,
        string postalCode,
        string taxIdCode,
        string taxIdValue)
    {
        // Arrange
        var sut = new UpgradePremiumToOrganizationRequest
        {
            OrganizationName = "International Business",
            Key = "encrypted-key",
            TargetProductTierType = tierType,
            PublicKey = "public-key",
            EncryptedPrivateKey = "encrypted-private-key",
            CollectionName = "Default Collection",
            BillingAddress = new CheckoutBillingAddressRequest
            {
                Country = country,
                PostalCode = postalCode,
                TaxId = new CheckoutBillingAddressRequest.TaxIdRequest
                {
                    Code = taxIdCode,
                    Value = taxIdValue
                }
            }
        };

        // Act
        var upgrade = sut.ToDomain();

        // Assert
        Assert.Equal("International Business", upgrade.OrganizationName);
        Assert.Equal("encrypted-key", upgrade.Key);
        Assert.Equal("public-key", upgrade.PublicKey);
        Assert.Equal("encrypted-private-key", upgrade.EncryptedPrivateKey);
        Assert.Equal("Default Collection", upgrade.CollectionName);
        Assert.Equal(expectedPlanType, upgrade.TargetPlanType);
        Assert.Equal(country, upgrade.BillingAddress.Country);
        Assert.Equal(postalCode, upgrade.BillingAddress.PostalCode);
        Assert.NotNull(upgrade.BillingAddress.TaxId);
        Assert.Equal(taxIdCode, upgrade.BillingAddress.TaxId.Code);
        Assert.Equal(taxIdValue, upgrade.BillingAddress.TaxId.Value);
    }
}
