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
        Products = new[] { ProductType.PasswordManager },
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
}
