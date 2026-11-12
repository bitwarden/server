using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Kdf;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Identity.IdentityServer;
using Bit.Identity.IdentityServer.RequestValidators;
using Bit.IntegrationTestCommon;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Constants;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Identity.IntegrationTest.Helpers;

/// <summary>
/// Arrange helpers for tests that log in by redeeming an SSO authorization code.
/// </summary>
public static class SsoLoginTestHelpers
{
    public const string TestEmail = "sso_user@email.com";

    public static async Task<JsonDocument> RunSuccessTestAsync(MemberDecryptionType memberDecryptionType)
    {
        return await RunSuccessTestAsync(factory => Task.CompletedTask, memberDecryptionType);
    }

    public static async Task<JsonDocument> RunSuccessTestAsync(Func<IdentityApplicationFactory, Task> configureFactory,
        MemberDecryptionType memberDecryptionType,
        string? keyConnectorUrl = null,
        bool trustedDeviceEnabled = true)
    {
        var challenge = new string('c', 50);
        var (factory, code) = await CreateFactoryAsync(new SsoConfigurationData
        {
            MemberDecryptionType = memberDecryptionType,
            KeyConnectorUrl = keyConnectorUrl,
        }, challenge, trustedDeviceEnabled);

        await configureFactory(factory);
        var context = await factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                { "scope", "api offline_access" },
                { "client_id", SsoAuthorizationCodeTestHelpers.ClientId },
                { "deviceType", "10" },
                { "deviceIdentifier", "test_id" },
                { "deviceName", "firefox" },
                { "twoFactorToken", "TEST" },
                { "twoFactorProvider", "5" }, // RememberMe Provider
                { "twoFactorRemember", "0" },
                { "grant_type", "authorization_code" },
                { "code", code },
                { "code_verifier", challenge },
                { "redirect_uri", SsoAuthorizationCodeTestHelpers.RedirectUri }
            }));

        // If this fails, surface detailed error information to aid debugging
        if (context.Response.StatusCode != StatusCodes.Status200OK)
        {
            string contentType = context.Response.ContentType ?? string.Empty;
            string rawBody = "<unreadable>";
            try
            {
                if (context.Response.Body.CanSeek)
                {
                    context.Response.Body.Position = 0;
                }
                using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
                rawBody = await reader.ReadToEndAsync();
            }
            catch
            {
                // leave rawBody as unreadable
            }

            string? error = null;
            string? errorDesc = null;
            string? errorModelMsg = null;
            try
            {
                using var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement;
                if (root.TryGetProperty("error", out var e)) error = e.GetString();
                if (root.TryGetProperty("error_description", out var ed)) errorDesc = ed.GetString();
                if (root.TryGetProperty("ErrorModel", out var em) && em.ValueKind == JsonValueKind.Object)
                {
                    if (em.TryGetProperty("Message", out var msg) && msg.ValueKind == JsonValueKind.String)
                    {
                        errorModelMsg = msg.GetString();
                    }
                }
            }
            catch
            {
                // Not JSON, continue with raw body
            }

            var message =
                $"Unexpected status {context.Response.StatusCode}." +
                $" error='{error}' error_description='{errorDesc}' ErrorModel.Message='{errorModelMsg}'" +
                $" ContentType='{contentType}' RawBody='{rawBody}'";
            Assert.Fail(message);
        }

        // Only calls that result in a 200 OK should call this helper
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        return await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
    }

    /// <summary>
    /// Seeds an SSO user and organization, and stores an authorization code for them through the real code store.
    /// </summary>
    /// <returns>The factory, and the authorization code to redeem with <paramref name="challenge"/>.</returns>
    public static async Task<(IdentityApplicationFactory Factory, string Code)> CreateFactoryAsync(
        SsoConfigurationData ssoConfigurationData,
        string challenge,
        bool trustedDeviceEnabled = true,
        Permissions? permissions = null)
    {
        var factory = new IdentityApplicationFactory();

        // Bypass client version gating to isolate SSO test behavior
        factory.SubstituteService<IClientVersionValidator>(svc =>
        {
            svc.Validate(Arg.Any<User>(), Arg.Any<CustomValidatorRequestContext>())
                .Returns(true);
        });

        var user = await factory.RegisterNewIdentityFactoryUserAsync(
            new RegisterFinishRequestModel
            {
                Email = TestEmail,
                MasterPasswordHash = "masterPasswordHash",
                Kdf = KdfType.PBKDF2_SHA256,
                KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
                UserAsymmetricKeys = new KeysRequestModel()
                {
                    PublicKey = TestEncryptionConstants.PublicKey,
                    EncryptedPrivateKey = TestEncryptionConstants.AES256_CBC_HMAC_Encstring // v1-format so parsing succeeds and user is treated as v1
                },
                UserSymmetricKey = TestEncryptionConstants.AES256_CBC_HMAC_Encstring,
            });

        var organizationRepository = factory.Services.GetRequiredService<IOrganizationRepository>();
        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = "billing-email@example.com",
            Plan = "Enterprise",
            UsePolicies = true,
        });

        var organizationUserRepository = factory.Services.GetRequiredService<IOrganizationUserRepository>();

        var orgUserPermissions =
            (permissions == null) ? null : JsonSerializer.Serialize(permissions, JsonHelpers.CamelCase);

        var organizationUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
            Permissions = orgUserPermissions
        });

        var ssoConfigRepository = factory.Services.GetRequiredService<ISsoConfigRepository>();
        await ssoConfigRepository.CreateAsync(new SsoConfig
        {
            OrganizationId = organization.Id,
            Enabled = true,
            Data = JsonSerializer.Serialize(ssoConfigurationData, JsonHelpers.CamelCase),
        });

        var code = await factory.StoreSsoAuthorizationCodeAsync(user.Id, TestEmail, organization.Id, challenge);

        return (factory, code);
    }

    public static async Task UpdateUserAsync(IdentityApplicationFactory factory, Action<User> changeUser)
    {
        var userRepository = factory.Services.GetRequiredService<IUserRepository>();
        var user = await userRepository.GetByEmailAsync(TestEmail);
        Assert.NotNull(user);
        changeUser(user);

        await userRepository.ReplaceAsync(user);
    }
}
