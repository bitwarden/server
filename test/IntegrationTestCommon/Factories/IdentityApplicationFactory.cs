using System.Net.Http.Json;
using System.Text.Json;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Identity;
using Bit.Identity.Models.Request.Accounts;
using Bit.Test.Common.Helpers;
using HandlebarsDotNet;
using Microsoft.AspNetCore.Http;

namespace Bit.IntegrationTestCommon.Factories;

public class IdentityApplicationFactory : WebApplicationFactoryBase<Startup>
{
    public const string DefaultDeviceIdentifier = "92b9d953-b9b6-4eaf-9d3e-11d57144dfeb";

    public async Task<HttpContext> RegisterAsync(RegisterRequestModel model)
    {
        return await Server.PostAsync("/accounts/register", JsonContent.Create(model));
    }

    public async Task<HttpContext> PostRegisterSendEmailVerificationAsync(RegisterSendVerificationEmailRequestModel model)
    {
        return await Server.PostAsync("/accounts/register/send-verification-email", JsonContent.Create(model));
    }

    public async Task<HttpContext> PostRegisterFinishAsync(RegisterFinishRequestModel model)
    {
        return await Server.PostAsync("/accounts/register/finish", JsonContent.Create(model));
    }

    public async Task<HttpContext> PostRegisterVerificationEmailClicked(RegisterVerificationEmailClickedRequestModel model)
    {
        return await Server.PostAsync("/accounts/register/verification-email-clicked", JsonContent.Create(model));
    }

    public async Task<(string Token, string RefreshToken)> TokenFromPasswordAsync(
        string username,
        string password,
        string deviceIdentifier = DefaultDeviceIdentifier,
        string clientId = "web",
        DeviceType deviceType = DeviceType.FirefoxBrowser,
        string deviceName = "firefox")
    {
        var context = await ContextFromPasswordAsync(
            username, password, deviceIdentifier, clientId, deviceType, deviceName);

        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        return (root.GetProperty("access_token").GetString(), root.GetProperty("refresh_token").GetString());
    }

    public async Task<HttpContext> ContextFromPasswordAsync(
        string username,
        string password,
        string deviceIdentifier = DefaultDeviceIdentifier,
        string clientId = "web",
        DeviceType deviceType = DeviceType.FirefoxBrowser,
        string deviceName = "firefox")
    {
        var context = await Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", clientId },
            { "deviceType", ((int)deviceType).ToString() },
            { "deviceIdentifier", deviceIdentifier },
            { "deviceName", deviceName },
            { "grant_type", "password" },
            { "username", username },
            { "password", password },
        }), context => context.Request.Headers.Append("Auth-Email", CoreHelpers.Base64UrlEncodeString(username)));

        return context;
    }

    public async Task<HttpContext> ContextFromPasswordWithTwoFactorAsync(
        string username,
        string password,
        string deviceIdentifier = DefaultDeviceIdentifier,
        string clientId = "web",
        DeviceType deviceType = DeviceType.FirefoxBrowser,
        string deviceName = "firefox",
        string twoFactorProviderType = "Email",
        string twoFactorToken = "two-factor-token")
    {
        var context = await Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", clientId },
            { "deviceType", ((int)deviceType).ToString() },
            { "deviceIdentifier", deviceIdentifier },
            { "deviceName", deviceName },
            { "grant_type", "password" },
            { "username", username },
            { "password", password },
            { "TwoFactorToken", twoFactorToken },
            { "TwoFactorProvider", twoFactorProviderType },
            { "TwoFactorRemember", "1" },
        }), context => context.Request.Headers.Append("Auth-Email", CoreHelpers.Base64UrlEncodeString(username)));

        return context;
    }

    public async Task<string> TokenFromAccessTokenAsync(Guid clientId, string clientSecret,
        DeviceType deviceType = DeviceType.SDK)
    {
        var context = await ContextFromAccessTokenAsync(clientId, clientSecret, deviceType);

        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        return root.GetProperty("access_token").GetString();
    }

    public async Task<HttpContext> ContextFromAccessTokenAsync(Guid clientId, string clientSecret,
        DeviceType deviceType = DeviceType.SDK)
    {
        var context = await Server.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "scope", "api.secrets" },
                { "client_id", clientId.ToString() },
                { "client_secret", clientSecret },
                { "grant_type", "client_credentials" },
                { "deviceType", ((int)deviceType).ToString() }
            }));

        return context;
    }

    public async Task<string> TokenFromOrganizationApiKeyAsync(string clientId, string clientSecret,
        DeviceType deviceType = DeviceType.FirefoxBrowser)
    {
        var context = await ContextFromOrganizationApiKeyAsync(clientId, clientSecret, deviceType);

        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        return root.GetProperty("access_token").GetString();
    }

    public async Task<HttpContext> ContextFromOrganizationApiKeyAsync(string clientId, string clientSecret,
        DeviceType deviceType = DeviceType.FirefoxBrowser)
    {
        var context = await Server.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "scope", "api.organization" },
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "grant_type", "client_credentials" },
                { "deviceType", ((int)deviceType).ToString() }
            }));
        return context;
    }
}
