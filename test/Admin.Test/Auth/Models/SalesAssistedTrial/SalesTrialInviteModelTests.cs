using System.ComponentModel.DataAnnotations;
using Bit.Admin.Auth.Models.SalesAssistedTrial;
using Bit.Core.Billing.Enums;

namespace Admin.Test.Auth.Models.SalesAssistedTrial;

public class SalesTrialInviteModelTests
{
    private static SalesTrialInviteModel BuildValidModel() => new()
    {
        Email = "prospect@example.com",
        Name = "Prospect Company",
        ProductTier = ProductTierType.Enterprise,
        Products = new[] { ProductType.PasswordManager },
        TrialLength = 30,
        PaymentOptional = true
    };

    [Fact]
    public void Validate_WhenPaymentOptionalAndTrialLengthZero_ReturnsError()
    {
        var model = BuildValidModel();
        model.TrialLength = 0;
        model.PaymentOptional = true;

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(results);
        Assert.Contains("Payment cannot be optional", results[0].ErrorMessage);
        Assert.Contains(nameof(model.PaymentOptional), results[0].MemberNames);
    }

    [Fact]
    public void Validate_WhenPaymentNotOptionalAndTrialLengthZero_NoError()
    {
        var model = BuildValidModel();
        model.TrialLength = 0;
        model.PaymentOptional = false;

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Empty(results);
    }

    [Fact]
    public void Validate_WhenPaymentOptionalAndTrialLengthNonZero_NoError()
    {
        var model = BuildValidModel();
        model.TrialLength = 14;
        model.PaymentOptional = true;

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Empty(results);
    }

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
    public void Validate_WhenProductTierIsNotTeamsStarter_NoError(ProductTierType productTier)
    {
        var model = BuildValidModel();
        model.ProductTier = productTier;

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Empty(results);
    }
}
