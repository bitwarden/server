using System.Globalization;
using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.IntegrationTestCommon;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.TwoFactorLoginTestHelpers;

namespace Bit.Identity.IntegrationTest.TokenEndpoint.TwoFactor;

public class TwoFactorLoginGrantApplicabilityTests : IClassFixture<IdentityApplicationFactory>
{
    private readonly IdentityApplicationFactory _factory;

    public TwoFactorLoginGrantApplicabilityTests(IdentityApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypeClientCredential_OrgTwoFactorRequired_Success(Organization organization, OrganizationApiKey organizationApiKey)
    {
        // Arrange
        organization.Enabled = true;
        organization.UseApi = true;
        organization.Use2fa = true;
        organization.TwoFactorProviders = OrganizationTwoFactor;

        var orgRepo = _factory.Services.GetRequiredService<IOrganizationRepository>();
        organization = await orgRepo.CreateAsync(organization);

        organizationApiKey.OrganizationId = organization.Id;
        organizationApiKey.Type = OrganizationApiKeyType.Default;

        var orgApiKeyRepo = _factory.Services.GetRequiredService<IOrganizationApiKeyRepository>();
        await orgApiKeyRepo.CreateAsync(organizationApiKey);

        // Act
        var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", $"organization.{organization.Id}" },
            { "client_secret", organizationApiKey.ApiKey },
            { "scope", "api.organization" },
        }));

        // Assert
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;
        var token = AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String).GetString();
        Assert.NotNull(token);
    }

    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypeClientCredential_IndvTwoFactorRequired_Success(string deviceId)
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        await CreateUserAsync(localFactory, TestEmail, UserEmailTwoFactor);

        var database = localFactory.GetDatabaseContext();
        var user = await database.Users.FirstAsync(u => u.Email == TestEmail);

        // Act
        var context = await localFactory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", $"user.{user.Id}" },
            { "client_secret", user.ApiKey },
            { "scope", "api" },
            { "DeviceIdentifier", deviceId },
            { "DeviceType",  ((int)DeviceType.FirefoxBrowser).ToString(CultureInfo.InvariantCulture) },
            { "DeviceName", "firefox" },
        }));

        // Assert
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;
        var token = AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String).GetString();
        Assert.NotNull(token);
    }

    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypeAuthCode_OrgTwoFactorRequired_IndvTwoFactor_NoTwoFactorProvided_Fails(string deviceId)
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        var challenge = new string('c', 50);
        var ssoConfigData = new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.MasterPassword,
        };
        var code = await CreateSsoOrganizationAndUserAsync(
            localFactory, ssoConfigData, challenge, TestEmail, userTwoFactor: UserEmailTwoFactor);

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

    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypeAuthCode_OrgTwoFactorRequired_IndvTwoFactor_TwoFactorProvided_Success(string deviceId)
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();

        // Create Test User
        var challenge = new string('c', 50);
        var ssoConfigData = new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.MasterPassword,
        };
        var code = await CreateSsoOrganizationAndUserAsync(
            localFactory, ssoConfigData, challenge, TestEmail, userTwoFactor: UserEmailTwoFactor);

        // Act
        var failedTokenContext = await localFactory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
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

        Assert.Equal(StatusCodes.Status400BadRequest, failedTokenContext.Response.StatusCode);
        var emailToken = await EmailLoginCodeAsync(localFactory, TestEmail, deviceId);

        var twoFactorProvidedContext = await localFactory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", SsoAuthorizationCodeTestHelpers.ClientId },
            { "deviceType", "12" },
            { "deviceIdentifier", deviceId },
            { "deviceName", "edge" },
            { "twoFactorToken", emailToken},
            { "twoFactorProvider", "1" },
            { "twoFactorRemember", "0" },
            { "grant_type", "authorization_code" },
            { "code", code },
            { "code_verifier", challenge },
            { "redirect_uri", SsoAuthorizationCodeTestHelpers.RedirectUri }
        }));


        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(twoFactorProvidedContext);
        var root = body.RootElement;

        var result = AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String).GetString();
        Assert.NotNull(result);
    }
}
