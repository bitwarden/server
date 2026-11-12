using System.Text.Json;
using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Test.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.SsoLoginTestHelpers;

#nullable enable

namespace Bit.Identity.IntegrationTest.TokenEndpoint.DecryptionOptions;

public class SsoLoginMasterPasswordDecryptionOptionTests
{
    [Fact]
    public async Task Test_MasterPassword_DecryptionType()
    {
        // Arrange
        User? expectedUser = null;
        using var responseBody = await RunSuccessTestAsync(async factory =>
        {
            var database = factory.GetDatabaseContext();

            expectedUser = await database.Users.SingleAsync(u => u.Email == TestEmail);
            Assert.NotNull(expectedUser);
        }, MemberDecryptionType.MasterPassword);
        Assert.NotNull(expectedUser);

        // Assert
        // If the organization has a member decryption type of MasterPassword that should be the only option in the reply
        var root = responseBody.RootElement;
        AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String);
        var userDecryptionOptions = AssertHelper.AssertJsonProperty(root, "UserDecryptionOptions", JsonValueKind.Object);

        // Expected to look like:
        // "UserDecryptionOptions": {
        //   "Object": "userDecryptionOptions"
        //   "HasMasterPassword": true,
        //   "MasterPasswordUnlock": {
        //     "Kdf": {
        //       "KdfType": 0,
        //       "Iterations": 600000
        //     },
        //     "MasterKeyEncryptedUserKey": "2.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==",
        //     "Salt": "sso_user@email.com"
        //   }
        // }

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
        Assert.Equal(TestEmail, salt);
    }
}
