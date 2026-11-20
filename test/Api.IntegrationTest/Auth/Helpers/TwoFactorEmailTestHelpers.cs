using System.Globalization;
using System.Net;
using System.Text.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.IntegrationTestCommon;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Helpers;
using Core.Auth.Enums;
using Xunit;
using static Bit.Api.IntegrationTest.Auth.Helpers.TwoFactorTestHelpers;

namespace Bit.Api.IntegrationTest.Auth.Helpers;

/// <summary>
/// Stateless helpers for the email two-factor <c>TwoFactorController</c> integration tests: requesting emailed codes,
/// and logging in through the Identity host with a master password or an SSO authorization code.
/// </summary>
internal static class TwoFactorEmailTestHelpers
{
    private const string SsoRedirectUri = SsoAuthorizationCodeTestHelpers.RedirectUri;

    public sealed record SsoLogin(string AuthorizationCode, string CodeVerifier);

    public static async Task<string> GetEmailUserVerificationTokenAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/two-factor/get-email",
            new { MasterPasswordHash = MasterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var (_, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(response, "email");
        return uvToken;
    }

    /// <summary>Requests a setup code for the given address and returns the code that was emailed.</summary>
    public static async Task<string> EmailSetupCodeAsync(
        HttpClient client, IMailService mailService, string accountEmail, string twoFactorAddress, string uvToken)
    {
        var response = await client.PostAsJsonAsync("/two-factor/send-email",
            new { Email = twoFactorAddress, UserVerificationToken = uvToken });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return TwoFactorEmailAssertions.AssertCodeEmailed(mailService, accountEmail, TwoFactorEmailPurpose.Setup);
    }

    /// <summary>Requests a token without a two-factor code and asserts the two-factor challenge comes back.</summary>
    public static async Task ChallengeAsync(ApiApplicationFactory factory, string email, string deviceIdentifier)
    {
        var context = await factory.Identity.ContextFromPasswordAsync(
            email, MasterPasswordHash, deviceIdentifier);
        var root = await ReadIdentityJsonAsync(context);
        Assert.True(root.TryGetProperty("error_description", out var errorDescription), root.ToString());
        Assert.Equal("Two factor required.", errorDescription.GetString());
    }

    public static async Task<JsonElement> LogInWithTwoFactorAsync(
        ApiApplicationFactory factory, string email, string deviceIdentifier, string code)
    {
        var context = await factory.Identity.ContextFromPasswordWithTwoFactorAsync(
            email,
            MasterPasswordHash,
            deviceIdentifier,
            twoFactorProviderType: ((int)TwoFactorProviderType.Email).ToString(CultureInfo.InvariantCulture),
            twoFactorToken: code);
        return await ReadIdentityJsonAsync(context);
    }

    public static async Task<JsonElement> ReadIdentityJsonAsync(HttpContext context)
    {
        using var document = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        Assert.NotNull(document);
        return document.RootElement.Clone();
    }

    public static Dictionary<string, string?> MasterPasswordBody(string email, string? bodyDeviceIdentifier = null)
    {
        var body = new Dictionary<string, string?>
        {
            ["Email"] = email,
            ["MasterPasswordHash"] = MasterPasswordHash,
        };
        if (bodyDeviceIdentifier != null)
        {
            body["DeviceIdentifier"] = bodyDeviceIdentifier;
        }

        return body;
    }

    /// <summary>Calls send-email-login without a bearer token, as a client does in the middle of login.</summary>
    public static async Task<HttpResponseMessage> SendEmailLoginAsync(
        ApiApplicationFactory factory, Dictionary<string, string?> body, string? headerDeviceIdentifier)
    {
        using var client = factory.CreateClient();
        using var message = new HttpRequestMessage(HttpMethod.Post, "/two-factor/send-email-login");
        if (headerDeviceIdentifier != null)
        {
            message.Headers.Add("Device-Identifier", headerDeviceIdentifier);
        }

        message.Content = JsonContent.Create(body);
        return await client.SendAsync(message);
    }

    /// <summary>
    /// Puts the user in an organization that uses SSO and registers an authorization code that signs the user in
    /// through it.
    /// </summary>
    public static async Task<SsoLogin> ArrangeSsoLoginAsync(ApiApplicationFactory factory, User user, string email)
    {
        var organization = await factory.GetService<IOrganizationRepository>().CreateAsync(new Organization
        {
            Name = "Two Factor Email SSO Org",
            BillingEmail = "billing-email@example.com",
            Plan = "Enterprise",
            UsePolicies = true,
            UseSso = true,
            Use2fa = true,
        });

        await factory.GetService<IOrganizationUserRepository>().CreateAsync(new OrganizationUser
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
        });

        await factory.GetService<ISsoConfigRepository>().CreateAsync(new SsoConfig
        {
            OrganizationId = organization.Id,
            Enabled = true,
            Data = JsonSerializer.Serialize(
                new SsoConfigurationData { MemberDecryptionType = MemberDecryptionType.MasterPassword },
                JsonHelpers.CamelCase),
        });

        var codeVerifier = new string('c', 50);
        var authorizationCode = await factory.Identity.StoreSsoAuthorizationCodeAsync(
            user.Id, email, organization.Id, codeVerifier);

        return new SsoLogin(authorizationCode, codeVerifier);
    }

    public static async Task<JsonElement> PostSsoTokenAsync(
        ApiApplicationFactory factory, SsoLogin sso, string deviceIdentifier, string? twoFactorToken)
    {
        var form = new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", ((int)DeviceType.FirefoxBrowser).ToString(CultureInfo.InvariantCulture) },
            { "deviceIdentifier", deviceIdentifier },
            { "deviceName", "firefox" },
            { "grant_type", "authorization_code" },
            { "code", sso.AuthorizationCode },
            { "code_verifier", sso.CodeVerifier },
            { "redirect_uri", SsoRedirectUri },
        };
        if (twoFactorToken != null)
        {
            form["twoFactorToken"] = twoFactorToken;
            form["twoFactorProvider"] = ((int)TwoFactorProviderType.Email).ToString(CultureInfo.InvariantCulture);
            form["twoFactorRemember"] = "0";
        }

        using var content = new FormUrlEncodedContent(form);
        var context = await factory.Identity.Server.PostAsync("/connect/token", content);
        return await ReadIdentityJsonAsync(context);
    }
}
