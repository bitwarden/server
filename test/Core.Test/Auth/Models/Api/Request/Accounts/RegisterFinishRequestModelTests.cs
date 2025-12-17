using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Test.Common.AutoFixture;
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
    [SignatureKeyPairRequestModelCustomize]
    public void ToData_Returns_ToData(string email, string masterPasswordHint, KdfRequestModel kdfRequest, string masterPasswordAuthenticationHash, AccountKeysRequestModel accountKeysRequest, string userSymmetricKey, KeysRequestModel userAsymmetricKeys, KdfType kdf, int kdfIterations, int? kdfMemory, int? kdfParallelism)
    {
        // Arrange
        // V1 model and fields to be removed with
        // https://bitwarden.atlassian.net/browse/PM-27326
        var legacyModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordAuthenticationHash,
            MasterPasswordHint = masterPasswordHint,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            Kdf = kdf,
            KdfIterations = kdfIterations,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism
        };
        var newModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHint = masterPasswordHint,
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = kdfRequest,
                MasterPasswordAuthenticationHash = masterPasswordAuthenticationHash,
                Salt = email.ToLowerInvariant().Trim()
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdfRequest,
                MasterKeyWrappedUserKey = userSymmetricKey,
                Salt = email.ToLowerInvariant().Trim()
            },
            AccountKeys = accountKeysRequest
        };

        // Act
        var legacyData = legacyModel.ToData();
        var newData = newModel.ToData();

        // Assert
        Assert.False(legacyData.IsV2Encryption());
        Assert.Equal(legacyData.MasterPasswordUnlockData.Kdf.KdfType, kdf);
        Assert.Equal(legacyData.MasterPasswordUnlockData.Kdf.Iterations, kdfIterations);
        Assert.Equal(legacyData.MasterPasswordUnlockData.Kdf.Memory, kdfMemory);
        Assert.Equal(legacyData.MasterPasswordUnlockData.Kdf.Parallelism, kdfParallelism);
        Assert.Equal(legacyData.MasterPasswordUnlockData.MasterKeyWrappedUserKey, userSymmetricKey);
        Assert.Equal(legacyData.MasterPasswordUnlockData.Salt, email.ToLowerInvariant().Trim());
        Assert.Equal(legacyData.UserAccountKeysData.PublicKeyEncryptionKeyPairData.PublicKey, userAsymmetricKeys.PublicKey);
        Assert.Equal(legacyData.UserAccountKeysData.PublicKeyEncryptionKeyPairData.WrappedPrivateKey, userAsymmetricKeys.EncryptedPrivateKey);
        Assert.Equal(legacyData.MasterPasswordAuthenticationData.Kdf.KdfType, kdf);
        Assert.Equal(legacyData.MasterPasswordAuthenticationData.Kdf.Iterations, kdfIterations);
        Assert.Equal(legacyData.MasterPasswordAuthenticationData.Kdf.Memory, kdfMemory);
        Assert.Equal(legacyData.MasterPasswordAuthenticationData.Kdf.Parallelism, kdfParallelism);
        Assert.Equal(legacyData.MasterPasswordAuthenticationData.MasterPasswordAuthenticationHash, masterPasswordAuthenticationHash);
        Assert.Equal(legacyData.MasterPasswordAuthenticationData.Salt, email.ToLowerInvariant().Trim());
        

        Assert.True(newData.IsV2Encryption());
        Assert.Equal(newData.MasterPasswordUnlockData.Kdf, kdfRequest.ToData());
        Assert.Equal(newData.MasterPasswordUnlockData.MasterKeyWrappedUserKey, userSymmetricKey);
        Assert.Equal(newData.MasterPasswordUnlockData.Salt, email.ToLowerInvariant());
        Assert.Equal(newData.MasterPasswordAuthenticationData.Kdf, kdfRequest.ToData());
        Assert.Equal(newData.MasterPasswordAuthenticationData.MasterPasswordAuthenticationHash, masterPasswordAuthenticationHash);
        Assert.Equal(newData.MasterPasswordAuthenticationData.Salt, email.ToLowerInvariant().Trim());
        Assert.Equal(newData.UserAccountKeysData, accountKeysRequest.ToAccountKeysData());
    }

    [Theory]
    [BitAutoData]
    [SignatureKeyPairRequestModelCustomize]
    public void ToUser_Returns_User(string email, string masterPasswordHint, AccountKeysRequestModel accountKeysRequest, 
        KdfRequestModel kdfRequest, string masterPasswordAuthenticationHash, string userSymmetricKey, 
        KeysRequestModel userAsymmetricKeys, KdfType kdf, int kdfIterations, int? kdfMemory, int? kdfParallelism)
    {
        // Arrange
        // V1 model and fields to be removed with
        // https://bitwarden.atlassian.net/browse/PM-27326
        var legacyModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordAuthenticationHash,
            MasterPasswordHint = masterPasswordHint,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            Kdf = kdf,
            KdfIterations = kdfIterations,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism
        };
        var newModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHint = masterPasswordHint,
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = kdfRequest,
                MasterPasswordAuthenticationHash = masterPasswordAuthenticationHash,
                Salt = email.ToLowerInvariant().Trim()
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdfRequest,
                MasterKeyWrappedUserKey = userSymmetricKey,
                Salt = email.ToLowerInvariant().Trim()
            },
            AccountKeys = accountKeysRequest
        };

        // Act
        Assert.False(legacyModel.ToData().IsV2Encryption());
        var legacyResult = legacyModel.ToUser(false);
        Assert.True(newModel.ToData().IsV2Encryption());
        var newResult = newModel.ToUser(true);

        // Assert
        Assert.Equal(email, legacyResult.Email);
        Assert.Equal(masterPasswordHint, legacyResult.MasterPasswordHint);
        Assert.Equal(kdf, legacyResult.Kdf);
        Assert.Equal(kdfIterations, legacyResult.KdfIterations);
        Assert.Equal(kdfMemory, legacyResult.KdfMemory);
        Assert.Equal(kdfParallelism, legacyResult.KdfParallelism);
        Assert.Equal(userSymmetricKey, legacyResult.Key);
        Assert.Equal(userAsymmetricKeys.PublicKey, legacyResult.PublicKey);
        Assert.Equal(userAsymmetricKeys.EncryptedPrivateKey, legacyResult.PrivateKey);

        // V2 expected fields
        // all should be default/unset, with the exception of email and masterPasswordHint
        Assert.Equal(email, newResult.Email);
        Assert.Equal(masterPasswordHint, newResult.MasterPasswordHint);
        Assert.Equal(KdfType.PBKDF2_SHA256, newResult.Kdf);
        Assert.Equal(AuthConstants.PBKDF2_ITERATIONS.Default, newResult.KdfIterations);
        Assert.Null(newResult.KdfMemory);
        Assert.Null(newResult.KdfParallelism);
        Assert.Null(newResult.Key);
        Assert.Null(newResult.PublicKey);
        Assert.Null(newResult.PrivateKey);
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
    public void Validate_WhenAuthAndRootHashBothMissing_ReturnsMissingHashErrorOnly()
    {
        var model = new RegisterFinishRequestModel
        {
            Email = "user@example.com",
            UserAsymmetricKeys = new KeysRequestModel { PublicKey = "pk", EncryptedPrivateKey = "sk" },
            // Both MasterPasswordAuthentication and MasterPasswordHash are missing
            MasterPasswordAuthentication = null,
            MasterPasswordHash = null,
            // Provide valid root KDF to avoid root KDF errors
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default,
            EmailVerificationToken = "token" // avoid token error
        };

        var results = Validate(model);

        // Only the new missing hash error should be present
        Assert.Single(results);
        Assert.Equal($"{nameof(MasterPasswordAuthenticationDataRequestModel.MasterPasswordAuthenticationHash)} and {nameof(RegisterFinishRequestModel.MasterPasswordHash)} not found on request, one needs to be defined.", results[0].ErrorMessage);
        Assert.Contains(nameof(MasterPasswordAuthenticationDataRequestModel.MasterPasswordAuthenticationHash), results[0].MemberNames);
        Assert.Contains(nameof(RegisterFinishRequestModel.MasterPasswordHash), results[0].MemberNames);
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
