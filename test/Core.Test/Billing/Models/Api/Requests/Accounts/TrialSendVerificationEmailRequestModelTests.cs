using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models.Api.Requests.Accounts;
using Xunit;

namespace Bit.Core.Test.Billing.Models.Api.Requests.Accounts;

public class TrialSendVerificationEmailRequestModelTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(29)]
    [InlineData(30)]
    public void TrialLength_InRange_PassesValidation(int value)
    {
        var results = Validate(ValidModel(value));
        Assert.DoesNotContain(results, r => r.MemberNames.Contains(nameof(TrialSendVerificationEmailRequestModel.TrialLength)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(31)]
    [InlineData(100)]
    public void TrialLength_OutOfRange_FailsValidation(int value)
    {
        var results = Validate(ValidModel(value));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(TrialSendVerificationEmailRequestModel.TrialLength)));
    }

    [Fact]
    public void TrialLength_Null_PassesValidation()
    {
        var results = Validate(ValidModel(null));
        Assert.DoesNotContain(results, r => r.MemberNames.Contains(nameof(TrialSendVerificationEmailRequestModel.TrialLength)));
    }

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("user+tag@sub.domain.org")]
    public void Email_Valid_PassesValidation(string email)
    {
        var results = Validate(new TrialSendVerificationEmailRequestModel
        {
            Email = email,
            ProductTier = ProductTierType.Teams,
            Products = [ProductType.PasswordManager],
        });
        Assert.DoesNotContain(results, r => r.MemberNames.Contains(nameof(TrialSendVerificationEmailRequestModel.Email)));
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("missing@")]
    [InlineData("@nodomain")]
    public void Email_Invalid_FailsValidation(string email)
    {
        var results = Validate(new TrialSendVerificationEmailRequestModel
        {
            Email = email,
            ProductTier = ProductTierType.Teams,
            Products = [ProductType.PasswordManager],
        });
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(TrialSendVerificationEmailRequestModel.Email)));
    }

    [Fact]
    public void Email_TooLong_FailsValidation()
    {
        var results = Validate(new TrialSendVerificationEmailRequestModel
        {
            Email = new string('a', 251) + "@b.com",
            ProductTier = ProductTierType.Teams,
            Products = [ProductType.PasswordManager],
        });
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(TrialSendVerificationEmailRequestModel.Email)));
    }

    [Fact]
    public void Products_Null_FailsValidation()
    {
        var results = Validate(new TrialSendVerificationEmailRequestModel
        {
            Email = "test@example.com",
            ProductTier = ProductTierType.Teams,
            Products = null!,
        });
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(TrialSendVerificationEmailRequestModel.Products)));
    }

    [Fact]
    public void Products_Empty_FailsValidation()
    {
        var results = Validate(new TrialSendVerificationEmailRequestModel
        {
            Email = "test@example.com",
            ProductTier = ProductTierType.Teams,
            Products = [],
        });
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(TrialSendVerificationEmailRequestModel.Products)));
    }

    [Fact]
    public void Name_TooLong_FailsValidation()
    {
        var results = Validate(new TrialSendVerificationEmailRequestModel
        {
            Email = "test@example.com",
            ProductTier = ProductTierType.Teams,
            Products = [ProductType.PasswordManager],
            Name = new string('a', 51),
        });
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(TrialSendVerificationEmailRequestModel.Name)));
    }

    [Fact]
    public void Name_Null_PassesValidation()
    {
        var results = Validate(new TrialSendVerificationEmailRequestModel
        {
            Email = "test@example.com",
            ProductTier = ProductTierType.Teams,
            Products = [ProductType.PasswordManager],
            Name = null,
        });
        Assert.DoesNotContain(results, r => r.MemberNames.Contains(nameof(TrialSendVerificationEmailRequestModel.Name)));
    }

    private static List<ValidationResult> Validate(TrialSendVerificationEmailRequestModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }

    private static TrialSendVerificationEmailRequestModel ValidModel(int? trialLength) =>
        new()
        {
            Email = "test@example.com",
            ProductTier = ProductTierType.Teams,
            Products = [ProductType.PasswordManager],
            TrialLength = trialLength
        };

}
