using System.Text.Json;
using Bit.IntegrationTestCommon;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using static Bit.Identity.IntegrationTest.Helpers.TwoFactorLoginTestHelpers;

namespace Bit.Identity.IntegrationTest.TokenEndpoint.TwoFactor;

public class TwoFactorLoginOrganizationDuoTests
{
    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypePassword_OrgDuoTwoFactorRequired_NoTwoFactorProvided_Fails(string deviceId)
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        var challenge = new string('c', 50);
        var ssoConfigData = new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.MasterPassword,
        };
        await CreateSsoOrganizationAndUserAsync(
            localFactory, ssoConfigData, challenge, TestEmail, orgTwoFactor: OrganizationTwoFactor);

        // Act
        var context = await localFactory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", "12" },
            { "deviceIdentifier", deviceId },
            { "deviceName", "edge" },
            { "grant_type", "password" },
            { "username", TestEmail },
            { "password", TestPassword },
        }));

        // Assert
        using var responseBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = responseBody.RootElement;
        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Two factor required.", error);
    }

    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypeAuthCode_OrgTwoFactorRequired_OrgDuoTwoFactor_NoTwoFactorProvided_Fails(string deviceId)
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        var challenge = new string('c', 50);
        var ssoConfigData = new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.MasterPassword,
        };

        var code = await CreateSsoOrganizationAndUserAsync(
            localFactory, ssoConfigData, challenge, TestEmail, orgTwoFactor: OrganizationTwoFactor);

        // Act
        var context = await localFactory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", SsoAuthorizationCodeTestHelpers.ClientId },
            { "deviceType", "12" },
            { "deviceIdentifier", deviceId },
            { "deviceName", "edge" },
            { "grant_type", "authorization_code" },
            { "code", code },
            { "code_verifier", challenge },
            { "redirect_uri", SsoAuthorizationCodeTestHelpers.RedirectUri }
        }));

        // Assert
        using var responseBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = responseBody.RootElement;
        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Two factor required.", error);
    }
}
