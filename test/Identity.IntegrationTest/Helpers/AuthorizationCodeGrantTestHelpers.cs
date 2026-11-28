using System.Globalization;
using System.Text.Json;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.IntegrationTestCommon;
using Bit.IntegrationTestCommon.Factories;
using Duende.IdentityServer.Models;
using static Bit.Identity.IntegrationTest.Helpers.OrganizationTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.PasswordGrantTestHelpers;

#nullable enable

namespace Bit.Identity.IntegrationTest.Helpers;

/// <summary>
/// Arrange and act helpers for tests of the <c>authorization_code</c> grant, which completes an SSO login.
/// </summary>
public static class AuthorizationCodeGrantTestHelpers
{
    /// <summary>
    /// The PKCE code verifier of the authorization codes that these helpers store.
    /// </summary>
    public const string DefaultCodeVerifier = "code_verifier_of_valid_length_0123456789_0123456789";

    /// <summary>
    /// Registers the default user as a confirmed member of an organization that uses SSO with master password
    /// decryption, and stores an authorization code for an SSO login of that user through that organization.
    /// </summary>
    /// <param name="changeCode">Changes the authorization code before the store saves it.</param>
    /// <returns>The user, and the authorization code to redeem with <see cref="DefaultCodeVerifier"/>.</returns>
    public static async Task<(User User, string Code)> RegisterSsoUserWithAuthorizationCodeAsync(
        IdentityApplicationFactory factory, Action<AuthorizationCode>? changeCode = null)
    {
        var user = await RegisterUserAsync(factory);
        var membership = await CreateOrganizationWithConfirmedMemberAsync(factory, user.Id, OrganizationUserType.User);
        await factory.Services.GetRequiredService<ISsoConfigRepository>().CreateAsync(new SsoConfig
        {
            OrganizationId = membership.OrganizationId,
            Enabled = true,
            Data = JsonSerializer.Serialize(
                new SsoConfigurationData { MemberDecryptionType = MemberDecryptionType.MasterPassword },
                JsonHelpers.CamelCase),
        });

        var code = await factory.StoreSsoAuthorizationCodeAsync(
            user.Id, user.Email, membership.OrganizationId, DefaultCodeVerifier, changeCode);
        return (user, code);
    }

    /// <summary>
    /// Sends an <c>authorization_code</c> grant request to the token endpoint. A null code, code verifier, or redirect
    /// URI leaves that field out of the request.
    /// </summary>
    /// <param name="includeDevice">
    /// When <c>false</c>, the request omits the device type, identifier, and name.
    /// </param>
    public static async Task<HttpContext> PostAuthorizationCodeAsync(
        IdentityApplicationFactory factory,
        string? code,
        string? codeVerifier = DefaultCodeVerifier,
        string? redirectUri = SsoAuthorizationCodeTestHelpers.RedirectUri,
        string clientId = SsoAuthorizationCodeTestHelpers.ClientId,
        string deviceIdentifier = IdentityApplicationFactory.DefaultDeviceIdentifier,
        DeviceType deviceType = DefaultDeviceType,
        string deviceName = DefaultDeviceName,
        bool includeDevice = true)
    {
        var form = new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", clientId },
            { "grant_type", "authorization_code" },
        };
        AddIfPresent(form, "code", code);
        AddIfPresent(form, "code_verifier", codeVerifier);
        AddIfPresent(form, "redirect_uri", redirectUri);
        if (includeDevice)
        {
            form.Add("deviceType", ((int)deviceType).ToString(CultureInfo.InvariantCulture));
            form.Add("deviceIdentifier", deviceIdentifier);
            form.Add("deviceName", deviceName);
        }

        return await factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(form));
    }

    private static void AddIfPresent(Dictionary<string, string> form, string name, string? value)
    {
        if (value != null)
        {
            form.Add(name, value);
        }
    }
}
