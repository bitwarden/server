using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Api.Request.Accounts;
using Bit.Core.Utilities;
using Bit.Identity;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.Http;

namespace Bit.Test.Common.ApplicationFactories
{
    public class IdentityApplicationFactory : WebApplicationFactoryBase<Startup>
    {
        public const string DefaultDeviceIdentifier = "92b9d953-b9b6-4eaf-9d3e-11d57144dfeb";

        public async Task<HttpContext> RegisterAsync(RegisterRequestModel model)
        {
            return await Server.PostAsync("/accounts/register", JsonContent.Create(model));
        }

        public async Task<(string Token, string RefreshToken)> TokenFromPasswordAsync(string username, string password, string deviceIdentifier = DefaultDeviceIdentifier, string clientId = "web", DeviceType deviceType = DeviceType.FirefoxBrowser, string deviceName = "firefox")
        {
            var context = await Server.PostAsync("/connect/token", new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("scope", "api offline_access"),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("deviceType", ((int)deviceType).ToString()),
                new KeyValuePair<string, string>("deviceIdentifier", deviceIdentifier),
                new KeyValuePair<string, string>("deviceName", deviceName),
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password),
            }), context => context.Request.Headers.Add("Auth-Email", CoreHelpers.Base64UrlEncodeString(username)));

            using var body = await AssertHelper.ResponseIsAsync<JsonDocument>(context);
            var root = body.RootElement;

            return (root.GetProperty("access_token").GetString(), root.GetProperty("refresh_token").GetString());
        }
    }
}
