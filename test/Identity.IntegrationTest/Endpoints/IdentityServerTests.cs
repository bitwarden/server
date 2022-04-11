using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Api.Request.Accounts;
using Bit.Core.Utilities;
using Bit.Test.Common.ApplicationFactories;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bit.Identity.IntegrationTest.Endpoints
{
    public class IdentityServerTests : IClassFixture<IdentityApplicationFactory>
    {
        private readonly IdentityApplicationFactory _factory;

        public IdentityServerTests(IdentityApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task WellKnownEndpoint_Success()
        {
            var context = await _factory.Server.GetAsync("/.well-known/openid-configuration");

            using var body = await AssertHelper.ResponseIsAsync<JsonDocument>(context);
            var root = body.RootElement;
            Assert.Equal(JsonValueKind.Object, root.ValueKind);
            AssertHelper.AssertJsonProperty(root, "issuer", JsonValueKind.String);

            var scopesSupported = AssertHelper.AssertJsonProperty(root, "scopes_supported", JsonValueKind.Array)
                .EnumerateArray()
                .Select(je => je.GetString());

            Assert.Contains("api", scopesSupported);
            Assert.Contains("api.push", scopesSupported);
            Assert.Contains("api.licensing", scopesSupported);
            Assert.Contains("api.organization", scopesSupported);
            Assert.Contains("internal", scopesSupported);
            Assert.Contains("offline_access", scopesSupported);

            var claimsSupported = AssertHelper.AssertJsonProperty(root, "claims_supported", JsonValueKind.Array)
                .EnumerateArray()
                .Select(je => je.GetString());

            Assert.Contains("name", claimsSupported);
            Assert.Contains("email", claimsSupported);
            Assert.Contains("email_verified", claimsSupported);
            Assert.Contains("sstamp", claimsSupported);
            Assert.Contains("premium", claimsSupported);
            Assert.Contains("device", claimsSupported);
            Assert.Contains("orgowner", claimsSupported);
            Assert.Contains("orgadmin", claimsSupported);
            Assert.Contains("orgmanager", claimsSupported);
            Assert.Contains("orguser", claimsSupported);
            Assert.Contains("orgcustom", claimsSupported);
            Assert.Contains("providerprovideradmin", claimsSupported);
            Assert.Contains("providerserviceuser", claimsSupported);
            Assert.Contains("sub", claimsSupported);

            var grantTypesSupported = AssertHelper.AssertJsonProperty(root, "grant_types_supported", JsonValueKind.Array)
                .EnumerateArray()
                .Select(je => je.GetString());

            Assert.Contains("authorization_code", grantTypesSupported);
            Assert.Contains("client_credentials", grantTypesSupported);
            Assert.Contains("refresh_token", grantTypesSupported);
            Assert.Contains("implicit", grantTypesSupported);
            Assert.Contains("password", grantTypesSupported);
            Assert.Contains("urn:ietf:params:oauth:grant-type:device_code", grantTypesSupported);

            // QUESTION: What are other breaking changes from this configuration that we should assert
        }

        [Fact]
        public async Task TokenEndpoint_GrantTypePassword_Success()
        {
            var deviceId = "92b9d953-b9b6-4eaf-9d3e-11d57144dfeb";
            var username = "test+tokenpassword@email.com";

            await _factory.RegisterAsync(new RegisterRequestModel
            {
                Email = username,
                MasterPasswordHash = "master_password_hash"
            });

            var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "scope", "api offline_access" },
                { "client_id", "web" },
                { "deviceType", "10" },
                { "deviceIdentifier", deviceId },
                { "deviceName", "firefox" },
                { "grant_type", "password" },
                { "username", username },
                { "password", "master_password_hash" },
            }), context => context.Request.Headers.Add("Auth-Email", CoreHelpers.Base64UrlEncodeString(username)));

            using var body = await AssertDefaultTokenBodyAsync(context);
            var root = body.RootElement;
            AssertHelper.AssertJsonProperty(root, "ForcePasswordReset", JsonValueKind.False);
            AssertHelper.AssertJsonProperty(root, "ResetMasterPassword", JsonValueKind.False);
            var kdf = AssertHelper.AssertJsonProperty(root, "Kdf", JsonValueKind.Number).GetInt32();
            Assert.Equal(0, kdf);
            var kdfIterations = AssertHelper.AssertJsonProperty(root, "KdfIterations", JsonValueKind.Number).GetInt32();
            Assert.Equal(5000, kdfIterations);
        }

        [Fact]
        public async Task TokenEndpoint_GrantTypePassword_NoAuthEmailHeader_Fails()
        {
            var deviceId = "92b9d953-b9b6-4eaf-9d3e-11d57144dfeb";
            var username = "test+noauthemailheader@email.com";

            await _factory.RegisterAsync(new RegisterRequestModel
            {
                Email = username,
                MasterPasswordHash = "master_password_hash",
            });

            var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "scope", "api offline_access" },
                { "client_id", "web" },
                { "deviceType", "10" },
                { "deviceIdentifier", deviceId },
                { "deviceName", "firefox" },
                { "grant_type", "password" },
                { "username", username },
                { "password", "master_password_hash" },
            }));

            Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

            var body = await AssertHelper.ResponseIsAsync<JsonDocument>(context);
            var root = body.RootElement;

            var error = AssertHelper.AssertJsonProperty(root, "error", JsonValueKind.String).GetString();
            Assert.Equal("invalid_grant", error);
            AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String);
        }

        [Fact]
        public async Task TokenEndpoint_GrantTypePassword_InvalidBase64AuthEmailHeader_Fails()
        {
            var deviceId = "92b9d953-b9b6-4eaf-9d3e-11d57144dfeb";
            var username = "test+badauthheader@email.com";

            await _factory.RegisterAsync(new RegisterRequestModel
            {
                Email = username,
                MasterPasswordHash = "master_password_hash",
            });

            var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "scope", "api offline_access" },
                { "client_id", "web" },
                { "deviceType", "10" },
                { "deviceIdentifier", deviceId },
                { "deviceName", "firefox" },
                { "grant_type", "password" },
                { "username", username },
                { "password", "master_password_hash" },
            }), context => context.Request.Headers.Add("Auth-Email", "bad_value"));

            Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

            var body = await AssertHelper.ResponseIsAsync<JsonDocument>(context);
            var root = body.RootElement;

            var error = AssertHelper.AssertJsonProperty(root, "error", JsonValueKind.String).GetString();
            Assert.Equal("invalid_grant", error);
            AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String);
        }

        [Fact]
        public async Task TokenEndpoint_GrantTypePassword_WrongAuthEmailHeader_Fails()
        {
            var deviceId = "92b9d953-b9b6-4eaf-9d3e-11d57144dfeb";
            var username = "test+badauthheader@email.com";

            await _factory.RegisterAsync(new RegisterRequestModel
            {
                Email = username,
                MasterPasswordHash = "master_password_hash",
            });

            var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "scope", "api offline_access" },
                { "client_id", "web" },
                { "deviceType", "10" },
                { "deviceIdentifier", deviceId },
                { "deviceName", "firefox" },
                { "grant_type", "password" },
                { "username", username },
                { "password", "master_password_hash" },
            }), context => context.Request.Headers.Add("Auth-Email", CoreHelpers.Base64UrlEncodeString("bad_value")));

            Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

            var body = await AssertHelper.ResponseIsAsync<JsonDocument>(context);
            var root = body.RootElement;

            var error = AssertHelper.AssertJsonProperty(root, "error", JsonValueKind.String).GetString();
            Assert.Equal("invalid_grant", error);
            AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String);
        }

        [Fact]
        public async Task TokenEndpoint_GrantTypeRefreshToken_Success()
        {
            var deviceId = "5a7b19df-0c9d-46bf-a104-8034b5a17182";
            var username = "test+tokenrefresh@email.com";

            await _factory.RegisterAsync(new RegisterRequestModel
            {
                Email = username,
                MasterPasswordHash = "master_password_hash",
            });

            var (_, refreshToken) = await _factory.TokenFromPasswordAsync(username, "master_password_hash", deviceId);

            var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "client_id", "web" },
                { "refresh_token", refreshToken },
            }));

            await AssertDefaultTokenBodyAsync(context);
        }

        [Fact]
        public async Task TokenEndpoint_GrantTypeClientCredentials_Success()
        {
            var username = "test+tokenclientcredentials@email.com";
            var deviceId = "8f14a393-edfe-40ba-8c67-a856cb89c509";

            await _factory.RegisterAsync(new RegisterRequestModel
            {
                Email = username,
                MasterPasswordHash = "master_password_hash",
            });

            var database = _factory.GetDatabaseContext();
            var user = await database.Users
                .FirstAsync(u => u.Email == username);

            var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "client_id", $"user.{user.Id}" },
                { "client_secret", user.ApiKey },
                { "scope", "api" },
                { "DeviceIdentifier", deviceId },
                { "DeviceType", ((int)DeviceType.FirefoxBrowser).ToString() },
                { "DeviceName", "firefox" },
            }));

            using var body = await AssertHelper.ResponseIsAsync<JsonDocument>(context);
            var root = body.RootElement;

            Assert.Equal(JsonValueKind.Object, root.ValueKind);
            AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String);
            var expiresIn = AssertHelper.AssertJsonProperty(root, "expires_in", JsonValueKind.Number).GetInt32();
            Assert.Equal(3600, expiresIn);
            var tokenType = AssertHelper.AssertJsonProperty(root, "token_type", JsonValueKind.String).GetString();
            Assert.Equal("Bearer", tokenType);
            var scope = AssertHelper.AssertJsonProperty(root, "scope", JsonValueKind.String).GetString();
            Assert.Equal("api", scope);
        }

        private static async Task<JsonDocument> AssertDefaultTokenBodyAsync(HttpContext httpContext)
        {
            var body = await AssertHelper.ResponseIsAsync<JsonDocument>(httpContext);
            var root = body.RootElement;

            Assert.Equal(JsonValueKind.Object, root.ValueKind);
            AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String);
            var expiresIn = AssertHelper.AssertJsonProperty(root, "expires_in", JsonValueKind.Number).GetInt32();
            Assert.Equal(3600, expiresIn);
            var tokenType = AssertHelper.AssertJsonProperty(root, "token_type", JsonValueKind.String).GetString();
            Assert.Equal("Bearer", tokenType);
            AssertHelper.AssertJsonProperty(root, "refresh_token", JsonValueKind.String);
            var scope = AssertHelper.AssertJsonProperty(root, "scope", JsonValueKind.String).GetString();
            Assert.Equal("api offline_access", scope);

            return body;
        }
    }
}
