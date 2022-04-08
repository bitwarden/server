using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Bit.Core.Models.Api.Request.Accounts;
using Bit.Core.Utilities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Test.Common.ApplicationFactories;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.Identity.IntegrationTest.Controllers
{
    public class AccountsControllerTests : IClassFixture<IdentityApplicationFactory>
    {
        private readonly IdentityApplicationFactory _factory;

        public AccountsControllerTests(IdentityApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task PostRegister_Success()
        {
            var context = await _factory.RegisterAsync(new RegisterRequestModel
            {
                Email = "test+register@email.com",
                MasterPasswordHash = "master_password_hash"
            });

            Assert.Equal(200, context.Response.StatusCode);

            var database = _factory.GetDatabaseContext();
            var user = await database.Users
                .SingleAsync(u => u.Email == "test+register@email.com");

            Assert.NotNull(user);
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
        public async Task TokenEndpoint_Success()
        {
            var deviceId = "92b9d953-b9b6-4eaf-9d3e-11d57144dfeb";
            var username = "test+token@email.com";

            await _factory.RegisterAsync(new RegisterRequestModel
            {
                Email = username,
                MasterPasswordHash = "master_password_hash"
            });

            var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("scope", "api offline_access"),
                new KeyValuePair<string, string>("client_id", "web"),
                new KeyValuePair<string, string>("deviceType", "10"),
                new KeyValuePair<string, string>("deviceIdentifier", deviceId),
                new KeyValuePair<string, string>("deviceName", "firefox"),
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", "master_password_hash"),
            }), context => context.Request.Headers.Add("Auth-Email", CoreHelpers.Base64UrlEncodeString(username)));

            using var body = await AssertHelper.ResponseIsAsync<JsonDocument>(context);
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
            AssertHelper.AssertJsonProperty(root, "ForcePasswordReset", JsonValueKind.False);
            AssertHelper.AssertJsonProperty(root, "ResetMasterPassword", JsonValueKind.False);
            var kdf = AssertHelper.AssertJsonProperty(root, "Kdf", JsonValueKind.Number).GetInt32();
            Assert.Equal(0, kdf);
            var kdfIterations = AssertHelper.AssertJsonProperty(root, "KdfIterations", JsonValueKind.Number).GetInt32();
            Assert.Equal(5000, kdfIterations);
        }

        [Fact]
        public async Task AuthorizeEndpoint_Success()
        {
            var email = "test+authorize@email.com";

            await _factory.RegisterAsync(new RegisterRequestModel
            {
                Email = email,
                MasterPasswordHash = "master_password_hash"
            });

            var database = _factory.GetDatabaseContext();
            var user = await database.Users.SingleAsync(u => u.Email == email);

            var context = await _factory.Server.PostAsync("/connect/authorize", new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("client_id", "something")
            }));

            Assert.Equal(200, context.Response.StatusCode);
        }
    }
}
