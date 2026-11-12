using System.Text.Json;
using Bit.Test.Common.Helpers;
using static Bit.Identity.IntegrationTest.Helpers.SsoLoginTestHelpers;

#nullable enable

namespace Bit.Identity.IntegrationTest.TokenEndpoint.DecryptionOptions;

public class SsoLoginKeyConnectorDecryptionOptionTests
{
    [Fact]
    public async Task SsoLogin_KeyConnector_ReturnsOptions()
    {
        using var responseBody = await RunSuccessTestAsync(async factory =>
        {
            await UpdateUserAsync(factory, user => user.MasterPassword = null);
        }, MemberDecryptionType.KeyConnector, "https://key_connector.com");

        var root = responseBody.RootElement;
        AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String);

        var userDecryptionOptions = AssertHelper.AssertJsonProperty(root, "UserDecryptionOptions", JsonValueKind.Object);

        // Expected to look like:
        // "UserDecryptionOptions": {
        //   "Object": "userDecryptionOptions"
        //   "KeyConnectorOption": {
        //     "KeyConnectorUrl": "https://key_connector.com"
        //   }
        // }

        var keyConnectorOption = AssertHelper.AssertJsonProperty(userDecryptionOptions, "KeyConnectorOption", JsonValueKind.Object);

        var keyConnectorUrl = AssertHelper.AssertJsonProperty(keyConnectorOption, "KeyConnectorUrl", JsonValueKind.String).GetString();
        Assert.Equal("https://key_connector.com", keyConnectorUrl);
    }
}
