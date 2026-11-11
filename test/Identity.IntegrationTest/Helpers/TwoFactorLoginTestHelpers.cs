using System.Globalization;
using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Kdf;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.IntegrationTestCommon;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Fakes;
using Xunit;

namespace Bit.Identity.IntegrationTest.Helpers;

/// <summary>
/// Arrange helpers for tests of two-factor login.
/// </summary>
public static class TwoFactorLoginTestHelpers
{
    public const string OrganizationTwoFactor = """{"6":{"Enabled":true,"MetaData":{"ClientId":"DIEFB13LB49IEB3459N2","ClientSecret":"0ZnsZHav0KcNPBZTS6EOUwqLPoB0sfMd5aJeWExQ","Host":"api-example.duosecurity.com"}}}""";
    public const string TestEmail = "test+2farequired@email.com";
    public const string TestPassword = "master_password_hash";
    public const string AuthenticatorKey = "JBSWY3DPEHPK3PXP";
    public const string UserAuthenticatorTwoFactor =
        $$"""{"0": { "Enabled": true, "MetaData": { "Key": "{{AuthenticatorKey}}" } } }""";
    public const string UserEmailTwoFactor = """{"1": { "Enabled": true, "MetaData": { "Email": "test+2farequired@email.com"}}}""";

    // WebAuthn keys are persisted through JsonHelpers.LegacySerialize (Newtonsoft), which writes
    // Descriptor.Id as standard Base64, not Fido2NetLib's Base64Url - this Id contains both '+'
    // and '/'. Building the login challenge for this provider (WebAuthnTokenProvider.GenerateAsync
    // -> LoadKeys) decodes it through Fido2's Base64UrlConverter, which v4 tightened to reject
    // those characters unless relaxed decoding is enabled.
    public static readonly string UserWebAuthnTwoFactor = BuildUserWebAuthnTwoFactorJson();

    private static string BuildUserWebAuthnTwoFactorJson()
    {
        // PublicKey/UserHandle are never cryptographically validated in this flow (no assertion is
        // verified until the client responds to the challenge) - only their shape matters. Use a
        // real COSE_Key CBOR-encoded ECDSA P-256 public key (what Fido2NetLib actually stores)
        // instead of a placeholder, so the fixture matches production data.
        using var authenticator = new FakeWebAuthnAuthenticator();
        var publicKey = Convert.ToBase64String(authenticator.GetCosePublicKey());
        var userHandle = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        return "{\"7\":{\"Enabled\":true,\"MetaData\":{\"Key0\":{\"Name\":\"YubiKey\",\"Descriptor\":{\"Id\":\"RtCGgkCX5KOVz/9GaZxzxKHNEDQTW06jb4SlSt96DqA=\",\"Type\":0,\"Transports\":null},"
            + "\"PublicKey\":\"" + publicKey + "\",\"UserHandle\":\"" + userHandle + "\","
            + "\"SignatureCounter\":0,\"RegDate\":\"2024-01-01T00:00:00\",\"Migrated\":false,\"AaGuid\":\"00000000-0000-0000-0000-000000000000\"}}}}";
    }

    /// <summary>
    /// Emails a real login code for the given device through the Identity host's email service and returns it.
    /// </summary>
    public static async Task<string> EmailLoginCodeAsync(
        IdentityApplicationFactory factory, string email, string deviceIdentifier)
    {
        var user = await factory.GetService<IUserRepository>().GetByEmailAsync(email);
        await factory.GetService<ITwoFactorEmailService>().SendTwoFactorLoginEmailAsync(user, deviceIdentifier);
        return factory.TwoFactorEmailCodes[email];
    }

    public static string NewUniqueEmail() => $"two-factor-{Guid.NewGuid()}@bitwarden.com";

    public static string BuildUserEmailTwoFactor(string email) =>
        "{\"1\":{\"Enabled\":true,\"MetaData\":{\"Email\":\"" + email + "\"}}}";

    public static string ProviderKey(TwoFactorProviderType providerType) =>
        ((int)providerType).ToString(CultureInfo.InvariantCulture);

    public static async Task CreateUserAsync(
        IdentityApplicationFactory factory,
        string testEmail,
        string userTwoFactor = null)
    {
        // Create Test User
        var user = await factory.RegisterNewIdentityFactoryUserAsync(
            new RegisterFinishRequestModel
            {
                Email = testEmail,
                MasterPasswordHash = TestPassword,
                Kdf = KdfType.PBKDF2_SHA256,
                KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
                UserAsymmetricKeys = new KeysRequestModel()
                {
                    PublicKey = Bit.Test.Common.Constants.TestEncryptionConstants.PublicKey,
                    EncryptedPrivateKey = Bit.Test.Common.Constants.TestEncryptionConstants.AES256_CBC_HMAC_Encstring
                },
                UserSymmetricKey = Bit.Test.Common.Constants.TestEncryptionConstants.AES256_CBC_HMAC_Encstring,
            });
        Assert.NotNull(user);

        var userService = factory.GetService<IUserService>();
        var userRepository = factory.Services.GetRequiredService<IUserRepository>();
        if (userTwoFactor != null)
        {
            user.TwoFactorProviders = userTwoFactor;
            await userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.Email);
            user = await userRepository.GetByEmailAsync(testEmail);
            Assert.NotNull(user.TwoFactorProviders);
        }
    }

    /// <summary>
    /// Seeds an SSO user and organization, and stores an authorization code for them through the real code store.
    /// </summary>
    /// <returns>The authorization code to redeem with <paramref name="challenge"/>.</returns>
    public static async Task<string> CreateSsoOrganizationAndUserAsync(
        IdentityApplicationFactory factory,
        SsoConfigurationData ssoConfigurationData,
        string challenge,
        string testEmail,
        Guid? orgId = null,
        string orgTwoFactor = null,
        string userTwoFactor = null,
        Permissions permissions = null)
    {
        var user = await factory.RegisterNewIdentityFactoryUserAsync(
            new RegisterFinishRequestModel
            {
                Email = testEmail,
                MasterPasswordHash = TestPassword,
                Kdf = KdfType.PBKDF2_SHA256,
                KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
                UserAsymmetricKeys = new KeysRequestModel()
                {
                    PublicKey = Bit.Test.Common.Constants.TestEncryptionConstants.PublicKey,
                    EncryptedPrivateKey = Bit.Test.Common.Constants.TestEncryptionConstants.AES256_CBC_HMAC_Encstring
                },
                UserSymmetricKey = Bit.Test.Common.Constants.TestEncryptionConstants.AES256_CBC_HMAC_Encstring,
            });

        var userService = factory.GetService<IUserService>();
        if (userTwoFactor != null)
        {
            user.TwoFactorProviders = userTwoFactor;
            await userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.Email);
        }

        // Create Organization
        var organizationRepository = factory.Services.GetRequiredService<IOrganizationRepository>();
        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Id = orgId ?? Guid.NewGuid(),
            Name = "Test Org",
            BillingEmail = "billing-email@example.com",
            Plan = "Enterprise",
            UsePolicies = true,
            UseSso = true,
            Use2fa = !string.IsNullOrEmpty(userTwoFactor) || !string.IsNullOrEmpty(orgTwoFactor),
            TwoFactorProviders = orgTwoFactor,
        });

        if (orgTwoFactor != null)
        {
            factory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("globalSettings:Duo:AKey", "WJHB374KM3N5hglO9hniwbkibg$789EfbhNyLpNq1");
            });
        }

        // Register User to Organization
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

        // Configure SSO
        var ssoConfigRepository = factory.Services.GetRequiredService<ISsoConfigRepository>();
        await ssoConfigRepository.CreateAsync(new SsoConfig
        {
            OrganizationId = organization.Id,
            Enabled = true,
            Data = JsonSerializer.Serialize(ssoConfigurationData, JsonHelpers.CamelCase),
        });

        return await factory.StoreSsoAuthorizationCodeAsync(user.Id, testEmail, organization.Id, challenge);
    }
}
