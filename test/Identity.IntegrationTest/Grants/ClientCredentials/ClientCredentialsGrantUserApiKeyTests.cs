using System.Text.Json;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Enums;
using Bit.Core.Test.Auth.AutoFixture;
using Bit.Identity.IntegrationTest.Helpers;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.TokenEndpointTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.TokenResponseAssertions;

namespace Bit.Identity.IntegrationTest.Grants.ClientCredentials;

[SutProviderCustomize]
public class ClientCredentialsGrantUserApiKeyTests
{
    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task TokenEndpoint_GrantTypeClientCredentials_Success(RegisterFinishRequestModel model)
    {
        model.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        var localFactory = new IdentityApplicationFactory();
        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(model);

        var context = await localFactory.Server.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "client_id", $"user.{user.Id}" },
                { "client_secret", user.ApiKey },
                { "scope", "api" },
                { "DeviceIdentifier", IdentityApplicationFactory.DefaultDeviceIdentifier },
                { "DeviceType", DeviceTypeAsString(DeviceType.FirefoxBrowser) },
                { "DeviceName", "firefox" },
            })
        );

        await AssertDefaultTokenBodyAsync(context, "api");
    }

    /// <summary>
    /// A user API key login has no subject, so the user's stamp and device claims reach the access token as client
    /// claims. They must appear under their plain claim names, with no client claim prefix.
    /// </summary>
    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task TokenEndpoint_GrantTypeClientCredentials_AccessTokenHasStampAndDeviceClaims(
        RegisterFinishRequestModel model)
    {
        model.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        var localFactory = new IdentityApplicationFactory();
        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(model);

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", $"user.{user.Id}" },
            { "client_secret", user.ApiKey },
            { "scope", "api" },
            { "DeviceIdentifier", IdentityApplicationFactory.DefaultDeviceIdentifier },
            { "DeviceType", DeviceTypeAsString(DeviceType.FirefoxBrowser) },
            { "DeviceName", "firefox" },
        });
        var context = await localFactory.Server.PostAsync("/connect/token", content);

        using var body = await AssertDefaultTokenBodyAsync(context, "api");
        var accessToken = AssertAccessTokenExists(body.RootElement);
        AccessTokenAssertions.AssertDeviceClaims(
            accessToken, IdentityApplicationFactory.DefaultDeviceIdentifier, DeviceType.FirefoxBrowser);
        var payload = AccessTokenAssertions.ReadPayload(accessToken);
        Assert.Equal(user.SecurityStamp,
            AssertHelper.AssertJsonProperty(payload, Claims.SecurityStamp, JsonValueKind.String).GetString());
        Assert.DoesNotContain(payload.EnumerateObject(), property => property.Name.StartsWith("client_") &&
            property.Name != "client_id");
    }

    /// <summary>
    /// A user API key login that names no device is rejected, so no access token is ever issued without device
    /// claims.
    /// </summary>
    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task TokenEndpoint_GrantTypeClientCredentials_WithoutDevice_IsRejected(
        RegisterFinishRequestModel model)
    {
        model.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        var localFactory = new IdentityApplicationFactory();
        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(model);

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", $"user.{user.Id}" },
            { "client_secret", user.ApiKey },
            { "scope", "api" },
        });
        var context = await localFactory.Server.PostAsync("/connect/token", content);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        Assert.False(body.RootElement.TryGetProperty("access_token", out _));
        var errorModel = AssertHelper.AssertJsonProperty(body.RootElement, "ErrorModel", JsonValueKind.Object);
        Assert.Equal("no device information provided",
            AssertHelper.AssertJsonProperty(errorModel, "Message", JsonValueKind.String).GetString());
    }
}
