using System.Text.Json;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Kdf;
using Bit.Core.Test.Auth.AutoFixture;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.TokenEndpointTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.TokenResponseAssertions;

namespace Bit.Identity.IntegrationTest.Grants.Password;

[SutProviderCustomize]
public class PasswordGrantResponseTests
{
    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task TokenEndpoint_GrantTypePassword_Success(RegisterFinishRequestModel requestModel)
    {
        requestModel.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        var localFactory = new IdentityApplicationFactory();
        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        var context = await PostLoginAsync(localFactory.Server, user, requestModel.MasterPasswordHash);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var root = body.RootElement;
        AssertRefreshTokenExists(root);
        AssertHelper.AssertJsonProperty(root, "ForcePasswordReset", JsonValueKind.False);
        var kdf = AssertHelper.AssertJsonProperty(root, "Kdf", JsonValueKind.Number).GetInt32();
        Assert.Equal(0, kdf);
        var kdfIterations = AssertHelper.AssertJsonProperty(root, "KdfIterations", JsonValueKind.Number).GetInt32();
        Assert.Equal(KdfConstants.PBKDF2_ITERATIONS.Default, kdfIterations);
        AssertUserDecryptionOptions(root, user);
    }

    private static void AssertUserDecryptionOptions(JsonElement tokenResponse, User expectedUser)
    {
        var userDecryptionOptions =
            AssertHelper.AssertJsonProperty(tokenResponse, "UserDecryptionOptions", JsonValueKind.Object);

        AssertHelper.AssertJsonProperty(userDecryptionOptions, "HasMasterPassword", JsonValueKind.True);
        var objectString = AssertHelper.AssertJsonProperty(userDecryptionOptions, "Object", JsonValueKind.String).ToString();
        Assert.Equal("userDecryptionOptions", objectString);
        var masterPasswordUnlock = AssertHelper.AssertJsonProperty(userDecryptionOptions, "MasterPasswordUnlock", JsonValueKind.Object);
        // MasterPasswordUnlock.Kdf
        var kdf = AssertHelper.AssertJsonProperty(masterPasswordUnlock, "Kdf", JsonValueKind.Object);
        var kdfType = AssertHelper.AssertJsonProperty(kdf, "KdfType", JsonValueKind.Number).GetInt32();
        Assert.Equal((int)expectedUser.Kdf, kdfType);
        var kdfIterations = AssertHelper.AssertJsonProperty(kdf, "Iterations", JsonValueKind.Number).GetInt32();
        Assert.Equal(expectedUser.KdfIterations, kdfIterations);
        // MasterPasswordUnlock.MasterKeyEncryptedUserKey
        var masterKeyEncryptedUserKey = AssertHelper.AssertJsonProperty(masterPasswordUnlock, "MasterKeyEncryptedUserKey", JsonValueKind.String).ToString();
        Assert.Equal(expectedUser.Key, masterKeyEncryptedUserKey);
        // MasterPasswordUnlock.Salt
        var salt = AssertHelper.AssertJsonProperty(masterPasswordUnlock, "Salt", JsonValueKind.String).ToString();
        Assert.Equal(expectedUser.Email.ToLower(), salt);
    }
}
