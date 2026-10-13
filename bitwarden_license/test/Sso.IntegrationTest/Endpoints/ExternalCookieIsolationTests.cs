using System.Security.Claims;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.IntegrationTestCommon;
using Bit.IntegrationTestCommon.Factories;
using Bit.Sso.IntegrationTest.Utilities;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace Bit.Sso.IntegrationTest.Endpoints;

/// <summary>
/// Runs the Sso and Identity hosts side by side with one persistent distributed cache and one data protection
/// key ring, as deployed services share them, and checks how each host treats the other's external cookie.
/// </summary>
public class ExternalCookieIsolationTests : IDisposable
{
    /// <summary>
    /// The cookie scheme that the Sso app's external login handlers sign in to.
    /// </summary>
    private const string SsoExternalScheme = AuthenticationSchemes.BitwardenSsoExternalCookieAuthenticationScheme;

    private const string UserEmail = "external_cookie_isolation_user@email.com";

    private readonly DirectoryInfo _keyDirectory;
    private readonly SsoApplicationFactory _ssoFactory;
    private readonly IdentityApplicationFactory _identityFactory;

    public ExternalCookieIsolationTests()
    {
        _keyDirectory = Directory.CreateTempSubdirectory("bw-external-cookie-keys-");
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

        _ssoFactory = new SsoApplicationFactory();
        _ssoFactory.ConfigureServices(services => ShareHostInfrastructure(services, cache, _keyDirectory));

        _identityFactory = new IdentityApplicationFactory();
        _identityFactory.ConfigureServices(services => ShareHostInfrastructure(services, cache, _keyDirectory));
    }

    [Theory]
    [InlineData(JwtClaimTypes.Subject)]
    [InlineData(ClaimTypes.NameIdentifier)]
    public async Task IdentityExternalCallback_CookieWrittenBySsoApp_DoesNotSignIn(string userIdClaimType)
    {
        // Arrange
        var user = await _identityFactory.RegisterNewIdentityFactoryUserAsync(new RegisterFinishRequestModel
        {
            Email = UserEmail,
            UserAsymmetricKeys = new KeysRequestModel
            {
                PublicKey = "public-key",
                EncryptedPrivateKey = "encrypted-private-key",
            },
        });

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(userIdClaimType, user.Id.ToString())], "External"));
        var properties = new AuthenticationProperties
        {
            Items =
            {
                ["scheme"] = Guid.NewGuid().ToString(),
                ["return_url"] = "~/",
                ["state"] = "test-state",
                ["user_identifier"] = string.Empty,
            },
        };
        var ssoCookie = await ExternalCookieTestHelpers.SignInCookieAsync(
            _ssoFactory.Services, SsoExternalScheme, principal, properties);

        // The Sso app reads back its own cookie
        var ssoResult = await ExternalCookieTestHelpers.AuthenticateCookieAsync(
            _ssoFactory.Services, SsoExternalScheme, ssoCookie.Name, ssoCookie.Value);
        Assert.True(ssoResult.Succeeded);

        // Both hosts share the distributed cache and the data protection key ring
        Assert.Same(
            _ssoFactory.Services.GetRequiredKeyedService<IDistributedCache>("persistent"),
            _identityFactory.Services.GetRequiredKeyedService<IDistributedCache>("persistent"));
        const string protectorPurpose = "external-cookie-isolation-tests";
        var protectedPayload = _ssoFactory.Services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(protectorPurpose).Protect("payload");
        Assert.Equal("payload", _identityFactory.Services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(protectorPurpose).Unprotect(protectedPayload));

        var identityCookieName = ExternalCookieTestHelpers.GetCookieName(
            _identityFactory.Services, _identityFactory.GetIdentityExternalScheme());

        // Act
        var context = await _identityFactory.GetSsoExternalCallbackAsync(identityCookieName, ssoCookie.Value);
        var tokenSubject = await _identityFactory.GetAccessTokenSubjectAsync(context);

        // Assert
        Assert.Null(tokenSubject);
        Assert.False(_identityFactory.SetsIdentityServerSessionCookie(context));
        Assert.NotEqual(StatusCodes.Status302Found, context.Response.StatusCode);
    }

    public void Dispose()
    {
        _identityFactory.Dispose();
        _ssoFactory.Dispose();
        _keyDirectory.Delete(recursive: true);
    }

    private static void ShareHostInfrastructure(
        IServiceCollection services,
        IDistributedCache cache,
        DirectoryInfo keyDirectory)
    {
        var persistentCacheDescriptors = services
            .Where(d => d.ServiceType == typeof(IDistributedCache) && Equals(d.ServiceKey, "persistent"))
            .ToList();
        foreach (var descriptor in persistentCacheDescriptors)
        {
            services.Remove(descriptor);
        }

        services.AddKeyedSingleton("persistent", cache);
        services.AddDataProtection().PersistKeysToFileSystem(keyDirectory);
    }
}
