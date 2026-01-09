using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Auth.Models.Api.Request.Accounts;

public class RegisterFinishRequestModelTests
{
    private static List<System.ComponentModel.DataAnnotations.ValidationResult> Validate(RegisterFinishRequestModel model)
    {
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            model,
            new System.ComponentModel.DataAnnotations.ValidationContext(model),
            results,
            true);
        return results;
    }

    [Theory]
    [BitAutoData]
    public void GetTokenType_Returns_EmailVerification(string email, string masterPasswordHash,
        string userSymmetricKey, KeysRequestModel userAsymmetricKeys, KdfType kdf, int kdfIterations, string emailVerificationToken)
    {
        // Arrange
        var model = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            Kdf = kdf,
            KdfIterations = kdfIterations,
            EmailVerificationToken = emailVerificationToken
        };

        // Act
        Assert.Equal(RegisterFinishTokenType.EmailVerification, model.GetTokenType());
    }

    [Theory]
    [BitAutoData]
    public void GetTokenType_Returns_OrganizationInvite(string email, string masterPasswordHash,
        string userSymmetricKey, KeysRequestModel userAsymmetricKeys, KdfType kdf, int kdfIterations, string orgInviteToken, Guid organizationUserId)
    {
        // Arrange
        var model = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            Kdf = kdf,
            KdfIterations = kdfIterations,
            OrgInviteToken = orgInviteToken,
            OrganizationUserId = organizationUserId
        };

        // Act
        Assert.Equal(RegisterFinishTokenType.OrganizationInvite, model.GetTokenType());
    }

    [Theory]
    [BitAutoData]
    public void GetTokenType_Returns_OrgSponsoredFreeFamilyPlan(string email, string masterPasswordHash,
        string userSymmetricKey, KeysRequestModel userAsymmetricKeys, KdfType kdf, int kdfIterations, string orgSponsoredFreeFamilyPlanToken)
    {
        // Arrange
        var model = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            Kdf = kdf,
            KdfIterations = kdfIterations,
            OrgSponsoredFreeFamilyPlanToken = orgSponsoredFreeFamilyPlanToken
        };

        // Act
        Assert.Equal(RegisterFinishTokenType.OrgSponsoredFreeFamilyPlan, model.GetTokenType());
    }

    [Theory]
    [BitAutoData]
    public void GetTokenType_Returns_EmergencyAccessInvite(string email, string masterPasswordHash,
        string userSymmetricKey, KeysRequestModel userAsymmetricKeys, KdfType kdf, int kdfIterations, string acceptEmergencyAccessInviteToken, Guid acceptEmergencyAccessId)
    {
        // Arrange
        var model = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            Kdf = kdf,
            KdfIterations = kdfIterations,
            AcceptEmergencyAccessInviteToken = acceptEmergencyAccessInviteToken,
            AcceptEmergencyAccessId = acceptEmergencyAccessId
        };

        // Act
        Assert.Equal(RegisterFinishTokenType.EmergencyAccessInvite, model.GetTokenType());
    }

    [Theory]
    [BitAutoData]
    public void GetTokenType_Returns_ProviderInvite(string email, string masterPasswordHash,
        string userSymmetricKey, KeysRequestModel userAsymmetricKeys, KdfType kdf, int kdfIterations, string providerInviteToken, Guid providerUserId)
    {
        // Arrange
        var model = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            Kdf = kdf,
            KdfIterations = kdfIterations,
            ProviderInviteToken = providerInviteToken,
            ProviderUserId = providerUserId
        };

        // Act
        Assert.Equal(RegisterFinishTokenType.ProviderInvite, model.GetTokenType());
    }

    [Theory]
    [BitAutoData]
    public void GetTokenType_Returns_Invalid(string email, string masterPasswordHash,
        string userSymmetricKey, KeysRequestModel userAsymmetricKeys, KdfType kdf, int kdfIterations)
    {
        // Arrange
        var model = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            Kdf = kdf,
            KdfIterations = kdfIterations
        };

        // Act
        var result = Assert.Throws<InvalidOperationException>(() => model.GetTokenType());
        Assert.Equal("Invalid token type.", result.Message);
    }

    [Theory]
    [BitAutoData]
    public void ToUser_Returns_User(string email, string masterPasswordHash, string masterPasswordHint,
        string userSymmetricKey, KeysRequestModel userAsymmetricKeys, KdfType kdf, int kdfIterations,
        int? kdfMemory, int? kdfParallelism)
    {
        // Arrange
        var model = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            Kdf = kdf,
            KdfIterations = kdfIterations,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism
        };

        // Act
        var result = model.ToUser();

        // Assert
        Assert.Equal(email, result.Email);
        Assert.Equal(masterPasswordHint, result.MasterPasswordHint);
        Assert.Equal(kdf, result.Kdf);
        Assert.Equal(kdfIterations, result.KdfIterations);
        Assert.Equal(kdfMemory, result.KdfMemory);
        Assert.Equal(kdfParallelism, result.KdfParallelism);
        Assert.Equal(userSymmetricKey, result.Key);
        Assert.Equal(userAsymmetricKeys.PublicKey, result.PublicKey);
        Assert.Equal(userAsymmetricKeys.EncryptedPrivateKey, result.PrivateKey);
    }

    [Fact]
    public void Validate_WhenBothAuthAndRootHashProvidedButNotEqual_ReturnsMismatchError()
    {
        var model = new RegisterFinishRequestModel
        {
            Email = "user@example.com",
            MasterPasswordHash = "root-hash",
            UserAsymmetricKeys = new KeysRequestModel { PublicKey = "pk", EncryptedPrivateKey = "sk" },
            // Provide both unlock and authentication with valid KDF so only the mismatch rule fires
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = AuthConstants.PBKDF2_ITERATIONS.Default },
                MasterKeyWrappedUserKey = "wrapped",
                Salt = "salt"
            },
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = AuthConstants.PBKDF2_ITERATIONS.Default },
                MasterPasswordAuthenticationHash = "auth-hash", // different than root
                Salt = "salt"
            },
            // Provide any valid token so we don't fail token validation
            EmailVerificationToken = "token"
        };

        var results = Validate(model);

        Assert.Contains(results, r =>
            r.ErrorMessage == $"{nameof(MasterPasswordAuthenticationDataRequestModel.MasterPasswordAuthenticationHash)} and root level {nameof(RegisterFinishRequestModel.MasterPasswordHash)} provided and are not equal. Only provide one.");
    }

    [Fact]
    public void Validate_WhenAuthProvidedButUnlockMissing_ReturnsUnlockMissingError()
    {
        var model = new RegisterFinishRequestModel
        {
            Email = "user@example.com",
            UserAsymmetricKeys = new KeysRequestModel { PublicKey = "pk", EncryptedPrivateKey = "sk" },
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = AuthConstants.PBKDF2_ITERATIONS.Default },
                MasterPasswordAuthenticationHash = "auth-hash",
                Salt = "salt"
            },
            EmailVerificationToken = "token"
        };

        var results = Validate(model);

        Assert.Contains(results, r => r.ErrorMessage == "MasterPasswordUnlock not found on RequestModel");
    }

    [Fact]
    public void Validate_WhenUnlockProvidedButAuthMissing_ReturnsAuthMissingError()
    {
        var model = new RegisterFinishRequestModel
        {
            Email = "user@example.com",
            UserAsymmetricKeys = new KeysRequestModel { PublicKey = "pk", EncryptedPrivateKey = "sk" },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = AuthConstants.PBKDF2_ITERATIONS.Default },
                MasterKeyWrappedUserKey = "wrapped",
                Salt = "salt"
            },
            EmailVerificationToken = "token"
        };

        var results = Validate(model);

        Assert.Contains(results, r => r.ErrorMessage == "MasterPasswordAuthentication not found on RequestModel");
    }

    [Fact]
    public void Validate_WhenNeitherAuthNorUnlock_AndRootKdfMissing_ReturnsBothRootKdfErrors()
    {
        var model = new RegisterFinishRequestModel
        {
            Email = "user@example.com",
            UserAsymmetricKeys = new KeysRequestModel { PublicKey = "pk", EncryptedPrivateKey = "sk" },
            // No MasterPasswordUnlock, no MasterPasswordAuthentication
            // No root Kdf and KdfIterations to trigger both errors
            EmailVerificationToken = "token"
        };

        var results = Validate(model);

        Assert.Contains(results, r => r.ErrorMessage == $"{nameof(RegisterFinishRequestModel.Kdf)} not found on RequestModel");
        Assert.Contains(results, r => r.ErrorMessage == $"{nameof(RegisterFinishRequestModel.KdfIterations)} not found on RequestModel");
    }

    [Fact]
    public void Validate_WhenNeitherAuthNorUnlock_AndValidRootKdf_IsValid()
    {
        var model = new RegisterFinishRequestModel
        {
            Email = "user@example.com",
            UserAsymmetricKeys = new KeysRequestModel { PublicKey = "pk", EncryptedPrivateKey = "sk" },
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default,
            // Memory and Parallelism irrelevant for PBKDF2
            EmailVerificationToken = "token"
        };

        var results = Validate(model);

        Assert.DoesNotContain(results, r => r.ErrorMessage?.Contains("Kdf") == true);
        Assert.Empty(results.Where(r => r.ErrorMessage == "No valid registration token provided"));
    }

    [Fact]
    public void Validate_WhenAllFieldsValidWithSubModels_IsValid()
    {
        var model = new RegisterFinishRequestModel
        {
            Email = "user@example.com",
            UserAsymmetricKeys = new KeysRequestModel { PublicKey = "pk", EncryptedPrivateKey = "sk" },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = AuthConstants.PBKDF2_ITERATIONS.Default },
                MasterKeyWrappedUserKey = "wrapped",
                Salt = "salt"
            },
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = AuthConstants.PBKDF2_ITERATIONS.Default },
                MasterPasswordAuthenticationHash = "auth-hash",
                Salt = "salt"
            },
            EmailVerificationToken = "token"
        };

        var results = Validate(model);

        Assert.Empty(results);
    }

    [Fact]
    public void Validate_WhenNoValidRegistrationTokenProvided_ReturnsTokenErrorOnly()
    {
        var model = new RegisterFinishRequestModel
        {
            Email = "user@example.com",
            UserAsymmetricKeys = new KeysRequestModel { PublicKey = "pk", EncryptedPrivateKey = "sk" },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = AuthConstants.PBKDF2_ITERATIONS.Default },
                MasterKeyWrappedUserKey = "wrapped",
                Salt = "salt"
            },
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = AuthConstants.PBKDF2_ITERATIONS.Default },
                MasterPasswordAuthenticationHash = "auth-hash",
                Salt = "salt"
            }
            // No token fields set
        };

        var results = Validate(model);

        Assert.Single(results);
        Assert.Equal("No valid registration token provided", results[0].ErrorMessage);
    }
}
