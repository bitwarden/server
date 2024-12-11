using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Auth.Models.Api.Request.Accounts;

public class RegisterFinishRequestModelTests
{
    [Theory]
    [BitAutoData]
    public void GetTokenType_Returns_EmailVerification(
        string email,
        string masterPasswordHash,
        string userSymmetricKey,
        KeysRequestModel userAsymmetricKeys,
        KdfType kdf,
        int kdfIterations,
        string emailVerificationToken
    )
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
            EmailVerificationToken = emailVerificationToken,
        };

        // Act
        Assert.Equal(RegisterFinishTokenType.EmailVerification, model.GetTokenType());
    }

    [Theory]
    [BitAutoData]
    public void GetTokenType_Returns_OrganizationInvite(
        string email,
        string masterPasswordHash,
        string userSymmetricKey,
        KeysRequestModel userAsymmetricKeys,
        KdfType kdf,
        int kdfIterations,
        string orgInviteToken,
        Guid organizationUserId
    )
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
            OrganizationUserId = organizationUserId,
        };

        // Act
        Assert.Equal(RegisterFinishTokenType.OrganizationInvite, model.GetTokenType());
    }

    [Theory]
    [BitAutoData]
    public void GetTokenType_Returns_OrgSponsoredFreeFamilyPlan(
        string email,
        string masterPasswordHash,
        string userSymmetricKey,
        KeysRequestModel userAsymmetricKeys,
        KdfType kdf,
        int kdfIterations,
        string orgSponsoredFreeFamilyPlanToken
    )
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
            OrgSponsoredFreeFamilyPlanToken = orgSponsoredFreeFamilyPlanToken,
        };

        // Act
        Assert.Equal(RegisterFinishTokenType.OrgSponsoredFreeFamilyPlan, model.GetTokenType());
    }

    [Theory]
    [BitAutoData]
    public void GetTokenType_Returns_EmergencyAccessInvite(
        string email,
        string masterPasswordHash,
        string userSymmetricKey,
        KeysRequestModel userAsymmetricKeys,
        KdfType kdf,
        int kdfIterations,
        string acceptEmergencyAccessInviteToken,
        Guid acceptEmergencyAccessId
    )
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
            AcceptEmergencyAccessId = acceptEmergencyAccessId,
        };

        // Act
        Assert.Equal(RegisterFinishTokenType.EmergencyAccessInvite, model.GetTokenType());
    }

    [Theory]
    [BitAutoData]
    public void GetTokenType_Returns_ProviderInvite(
        string email,
        string masterPasswordHash,
        string userSymmetricKey,
        KeysRequestModel userAsymmetricKeys,
        KdfType kdf,
        int kdfIterations,
        string providerInviteToken,
        Guid providerUserId
    )
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
            ProviderUserId = providerUserId,
        };

        // Act
        Assert.Equal(RegisterFinishTokenType.ProviderInvite, model.GetTokenType());
    }

    [Theory]
    [BitAutoData]
    public void GetTokenType_Returns_Invalid(
        string email,
        string masterPasswordHash,
        string userSymmetricKey,
        KeysRequestModel userAsymmetricKeys,
        KdfType kdf,
        int kdfIterations
    )
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
        };

        // Act
        var result = Assert.Throws<InvalidOperationException>(() => model.GetTokenType());
        Assert.Equal("Invalid token type.", result.Message);
    }

    [Theory]
    [BitAutoData]
    public void ToUser_Returns_User(
        string email,
        string masterPasswordHash,
        string masterPasswordHint,
        string userSymmetricKey,
        KeysRequestModel userAsymmetricKeys,
        KdfType kdf,
        int kdfIterations,
        int? kdfMemory,
        int? kdfParallelism
    )
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
            KdfParallelism = kdfParallelism,
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
}
