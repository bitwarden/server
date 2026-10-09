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
        Products = [ProductType.PasswordManager],
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
    public void Validate_WhenProductTierIsFamiliesAndProductsIncludeSecretsManager_ReturnsError()
    {
        var model = BuildValidModel();
        model.ProductTier = ProductTierType.Families;
        model.Products = [ProductType.SecretsManager];

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(results);
        Assert.Contains("Families", results[0].ErrorMessage);
        Assert.Contains(nameof(model.Products), results[0].MemberNames);
    }

    [Fact]
    public void Validate_WhenProductsIncludePasswordManagerAndSecretsManager_ReturnsError()
    {
        var model = BuildValidModel();
        model.ProductTier = ProductTierType.Enterprise;
        model.Products = [ProductType.PasswordManager, ProductType.SecretsManager];

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(results);
        Assert.Contains("select Secrets Manager on its own", results[0].ErrorMessage);
        Assert.Contains(nameof(model.Products), results[0].MemberNames);
    }

    [Fact]
    public void Validate_WhenProductTierIsFreeAndProductsIncludeSecretsManager_NoError()
    {
        var model = BuildValidModel();
        model.ProductTier = ProductTierType.Free;
        model.Products = [ProductType.SecretsManager];

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Empty(results);
    }

    [Fact]
    public void Validate_WhenPrivilegedControlsWithoutPasswordManager_ReturnsError()
    {
        var model = BuildValidModel();
        model.Products = [ProductType.PrivilegedControls];

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(results);
        Assert.Contains("requires Password Manager", results[0].ErrorMessage);
        Assert.Contains(nameof(model.Products), results[0].MemberNames);
    }

    [Theory]
    [InlineData(ProductTierType.Free)]
    [InlineData(ProductTierType.Families)]
    [InlineData(ProductTierType.Teams)]
    public void Validate_WhenPrivilegedControlsOnNonEnterpriseTier_ReturnsError(ProductTierType productTier)
    {
        var model = BuildValidModel();
        model.ProductTier = productTier;
        model.Products = [ProductType.PasswordManager, ProductType.PrivilegedControls];

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(results);
        Assert.Contains("Password Manager Enterprise", results[0].ErrorMessage);
        Assert.Contains(nameof(model.ProductTier), results[0].MemberNames);
    }

    [Fact]
    public void Validate_WhenPrivilegedControlsWithSecretsManager_ReturnsError()
    {
        var model = BuildValidModel();
        model.Products = [ProductType.PasswordManager, ProductType.SecretsManager, ProductType.PrivilegedControls];

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Contains(results, r => r.ErrorMessage!.Contains("cannot be combined with Secrets Manager"));
        Assert.All(results, r => Assert.Contains(nameof(model.Products), r.MemberNames));
    }

    [Fact]
    public void Validate_WhenPasswordManagerAndPrivilegedControlsOnEnterprise_NoError()
    {
        var model = BuildValidModel();
        model.ProductTier = ProductTierType.Enterprise;
        model.Products = [ProductType.PasswordManager, ProductType.PrivilegedControls];

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Empty(results);
    }
}
