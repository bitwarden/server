using System.Net.Http.Json;
using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Identity;
using Bit.Identity.Models.Request;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.Http;

namespace Bit.IntegrationTestCommon.Factories;

public class IdentityApplicationFactory : WebApplicationFactoryBase<Startup>
{
    public const string DefaultDeviceIdentifier = "92b9d953-b9b6-4eaf-9d3e-11d57144dfeb";

    public async Task<HttpContext> RegisterAsync(RegisterRequestModel model)
    {
        return await Server.PostAsync("/accounts/register", JsonContent.Create(model));
    }

    public async Task<(string Token, string RefreshToken)> TokenFromPasswordAsync(string username,
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
        }), context => context.Request.Headers.Add("Auth-Email", CoreHelpers.Base64UrlEncodeString(username)));

        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        return (root.GetProperty("access_token").GetString(), root.GetProperty("refresh_token").GetString());
    }
}
