using System.ComponentModel.DataAnnotations;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Request.Organizations;

public class OrganizationNoPaymentCreateRequestTests
{
    [Fact]
    public void Validate_KeysMissing_FailsValidation()
    {
        var model = new OrganizationNoPaymentCreateRequest
        {
            Name = "Test Org",
            BillingEmail = "test@example.com",
            Key = "test-key",
            UseSecretsManager = false,
            Keys = null
        };

        var results = ValidateModel(model);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(OrganizationNoPaymentCreateRequest.Keys)));
    }

    [Fact]
    public void Validate_KeysPresent_PassesKeysValidation()
    {
        var model = new OrganizationNoPaymentCreateRequest
        {
            Name = "Test Org",
            BillingEmail = "test@example.com",
            Key = "test-key",
            UseSecretsManager = false,
            Keys = new OrganizationKeysRequestModel
            {
                PublicKey = "test-public-key",
                EncryptedPrivateKey = "test-encrypted-private-key"
            }
        };

        var results = ValidateModel(model);

        Assert.DoesNotContain(results, r => r.MemberNames.Contains(nameof(OrganizationNoPaymentCreateRequest.Keys)));
    }

    [Fact]
    public void Validate_KeysMissingPublicKey_FailsValidation()
    {
        var keys = new OrganizationKeysRequestModel
        {
            PublicKey = null,
            EncryptedPrivateKey = "test-encrypted-private-key"
        };

        var results = ValidateModel(keys);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(OrganizationKeysRequestModel.PublicKey)));
    }

    [Fact]
    public void Validate_KeysMissingEncryptedPrivateKey_FailsValidation()
    {
        var keys = new OrganizationKeysRequestModel
        {
            PublicKey = "test-public-key",
            EncryptedPrivateKey = null
        };

        var results = ValidateModel(keys);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(OrganizationKeysRequestModel.EncryptedPrivateKey)));
    }

    [Fact]
    public void Validate_TrialLength_BelowRange_FailsValidation()
    {
        var model = new OrganizationNoPaymentCreateRequest
        {
            Name = "Test Org",
            BillingEmail = "test@example.com",
            Key = "test-key",
            UseSecretsManager = false,
            Keys = new OrganizationKeysRequestModel
            {
                PublicKey = "test-public-key",
                EncryptedPrivateKey = "test-encrypted-private-key"
            },
            TrialLength = -1
        };

        var results = ValidateModel(model);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(OrganizationNoPaymentCreateRequest.TrialLength)));
    }

    [Fact]
    public void Validate_TrialLength_AboveRange_FailsValidation()
    {
        var model = new OrganizationNoPaymentCreateRequest
        {
            Name = "Test Org",
            BillingEmail = "test@example.com",
            Key = "test-key",
            UseSecretsManager = false,
            Keys = new OrganizationKeysRequestModel
            {
                PublicKey = "test-public-key",
                EncryptedPrivateKey = "test-encrypted-private-key"
            },
            TrialLength = 31
        };

        var results = ValidateModel(model);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(OrganizationNoPaymentCreateRequest.TrialLength)));
    }

    [Fact]
    public void Validate_TrialLength_WithinRange_PassesValidation()
    {
        var model = new OrganizationNoPaymentCreateRequest
        {
            Name = "Test Org",
            BillingEmail = "test@example.com",
            Key = "test-key",
            UseSecretsManager = false,
            Keys = new OrganizationKeysRequestModel
            {
                PublicKey = "test-public-key",
                EncryptedPrivateKey = "test-encrypted-private-key"
            },
            TrialLength = 14
        };

        var results = ValidateModel(model);

        Assert.DoesNotContain(results, r => r.MemberNames.Contains(nameof(OrganizationNoPaymentCreateRequest.TrialLength)));
    }

    [Fact]
    public void Validate_TrialLength_Null_PassesValidation()
    {
        var model = new OrganizationNoPaymentCreateRequest
        {
            Name = "Test Org",
            BillingEmail = "test@example.com",
            Key = "test-key",
            UseSecretsManager = false,
            Keys = new OrganizationKeysRequestModel
            {
                PublicKey = "test-public-key",
                EncryptedPrivateKey = "test-encrypted-private-key"
            },
            TrialLength = null
        };

        var results = ValidateModel(model);

        Assert.DoesNotContain(results, r => r.MemberNames.Contains(nameof(OrganizationNoPaymentCreateRequest.TrialLength)));
    }

    [Fact]
    public void ToOrganizationSignup_TrialLength_IsMapped()
    {
        var model = new OrganizationNoPaymentCreateRequest
        {
            Name = "Test Org",
            BillingEmail = "test@example.com",
            Key = "test-key",
            UseSecretsManager = false,
            Keys = new OrganizationKeysRequestModel
            {
                PublicKey = "test-public-key",
                EncryptedPrivateKey = "test-encrypted-private-key"
            },
            TrialLength = 14
        };

        var signup = model.ToOrganizationSignup(new Bit.Core.Entities.User());

        Assert.Equal(14, signup.TrialLength);
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }
}
