using System.ComponentModel.DataAnnotations;
using Bit.Admin.Auth.Models.SalesAssistedTrial;
using Bit.Core.Billing.Enums;

namespace Admin.Test.Auth.Models.SalesAssistedTrial;

public class SalesAssistedTrialInviteModelTests
{
    private static SalesAssistedTrialInviteModel BuildValidModel() => new()
    {
        Email = "prospect@example.com",
        Name = "Prospect Company",
        ProductTier = ProductTierType.Enterprise,
        Product = ProductType.PasswordManager,
        TrialLength = 30,
    };

    [Fact]
    public void Validate_WhenProductTierIsTeamsStarter_ReturnsError()
    {
        var model = BuildValidModel();
        model.ProductTier = ProductTierType.TeamsStarter;

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(results);
        Assert.Contains("Teams Starter", results[0].ErrorMessage);
        Assert.Contains(nameof(model.ProductTier), results[0].MemberNames);
    }

    [Theory]
    [InlineData(ProductTierType.Free)]
    [InlineData(ProductTierType.Families)]
    [InlineData(ProductTierType.Teams)]
    [InlineData(ProductTierType.Enterprise)]
    public void Validate_WhenProductTierIsFreeFamiliesTeamsOrEnterprise_NoError(ProductTierType productTier)
    {
        var model = BuildValidModel();
        model.ProductTier = productTier;

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Empty(results);
    }

    // Current constraint of Families plan, appears as validation in the model for
    // fail-fast feedback to tool users.
    // PM-41426
    [Fact]
    public void Validate_WhenProductTierIsFamiliesAndProductIsSecretsManager_ReturnsError()
    {
        var model = BuildValidModel();
        model.ProductTier = ProductTierType.Families;
        model.Product = ProductType.SecretsManager;

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(results);
        Assert.Contains("Families", results[0].ErrorMessage);
        Assert.Contains(nameof(model.Product), results[0].MemberNames);
    }

    [Fact]
    public void Validate_WhenProductTierIsFreeAndProductIsSecretsManager_NoError()
    {
        var model = BuildValidModel();
        model.ProductTier = ProductTierType.Free;
        model.Product = ProductType.SecretsManager;

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Empty(results);
    }
}
