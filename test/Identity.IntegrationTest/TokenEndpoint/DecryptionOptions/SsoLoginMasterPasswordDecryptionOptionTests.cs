using System.Text.Json;
using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Test.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.SsoLoginTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.TokenResponseAssertions;

#nullable enable

namespace Bit.Identity.IntegrationTest.TokenEndpoint.DecryptionOptions;

public class SsoLoginMasterPasswordDecryptionOptionTests
{
    [Fact]
    public async Task Test_MasterPassword_DecryptionType()
    {
        // Arrange
        // TODO: Dispose the factory. RunSuccessTestAsync creates a factory but does not return it, so no caller can
        // dispose it.
        User? expectedUser = null;
        using var responseBody = await RunSuccessTestAsync(async factory =>
        {
            var database = factory.GetDatabaseContext();

            expectedUser = await database.Users.SingleAsync(u => u.Email == TestEmail);
            // TODO: Remove this assertion. SingleAsync already throws when no user matches.
            Assert.NotNull(expectedUser);
        }, MemberDecryptionType.MasterPassword);
        Assert.NotNull(expectedUser);

        // Assert
        // If the organization has a member decryption type of MasterPassword that should be the only option in the reply
        // TODO: Assert that the reply has no other decryption option. The assertions below do not check that
        // TrustedDeviceOption and KeyConnectorOption are absent.
        var root = responseBody.RootElement;
        // TODO: Use AssertAccessTokenExists.
        AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String);
        AssertMasterPasswordDecryptionOptions(root,
            expectedKdfType: expectedUser.Kdf,
            expectedKdfIterations: expectedUser.KdfIterations,
            expectedMasterKeyEncryptedUserKey: expectedUser.Key!,
            expectedSalt: TestEmail);
    }
}
