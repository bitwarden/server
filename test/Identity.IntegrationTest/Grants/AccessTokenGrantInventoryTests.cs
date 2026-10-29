using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Test.Auth.AutoFixture;
using Bit.Identity.IdentityServer;
using Bit.Identity.IdentityServer.ClientProviders;
using Bit.Identity.IdentityServer.Enums;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Xunit;

#nullable enable

namespace Bit.Identity.IntegrationTest.Grants;

/// <summary>
/// Inventory of every way the Identity server can issue an access token.
/// </summary>
/// <remarks>
/// API endpoints read the requesting device from a user access token's device claims. Every grant that issues user
/// access tokens therefore needs integration tests that assert:
/// <list type="number">
///   <item>the login access token carries the device claims,</item>
///   <item>a refreshed access token keeps them, when the grant issues refresh tokens, and</item>
///   <item>a login that names no device is rejected without an access token.</item>
/// </list>
/// These tests fail when a grant type or client provider is added or removed. Classify the change below, and add
/// the device-claim tests for any new grant that issues user access tokens.
/// </remarks>
public class AccessTokenGrantInventoryTests
{
    /// <summary>
    /// Grant types the static clients allow, mapped to whether they issue user scoped access tokens.
    /// </summary>
    private static readonly Dictionary<string, bool> _staticClientGrantTypes = new()
    {
        [GrantType.ResourceOwnerPassword] = true,
        [GrantType.AuthorizationCode] = true,
        [CustomGrantTypes.WebAuthn] = true,
        [CustomGrantTypes.SendAccess] = false,
    };

    /// <summary>
    /// Client providers that build clients from a prefixed client id (<c>prefix.identifier</c>), mapped to whether
    /// their clients issue user access tokens. Every provider's clients use the client_credentials grant.
    /// </summary>
    private static readonly Dictionary<Type, bool> _clientProviders = new()
    {
        [typeof(UserClientProvider)] = true,
        [typeof(OrganizationClientProvider)] = false,
        [typeof(InstallationClientProvider)] = false,
        [typeof(InternalClientProvider)] = false,
        [typeof(SecretsManagerApiKeyProvider)] = false,
        [typeof(PamAccessConnectorClientProvider)] = false,
    };

    [Fact]
    public void StaticClients_AllowOnlyInventoriedGrantTypes()
    {
        using var factory = new IdentityApplicationFactory();

        var allowedGrantTypes = factory.Services.GetRequiredService<StaticClientStore>().Clients.Values
            .SelectMany(client => client.AllowedGrantTypes)
            .ToHashSet();

        Assert.Equal(_staticClientGrantTypes.Keys.Order(), allowedGrantTypes.Order());
    }

    [Fact]
    public void ExtensionGrantValidators_AreInventoried()
    {
        using var factory = new IdentityApplicationFactory();
        using var scope = factory.Services.CreateScope();

        var extensionGrantTypes = scope.ServiceProvider.GetServices<IExtensionGrantValidator>()
            .Select(validator => validator.GrantType)
            .ToHashSet();

        Assert.Equal([CustomGrantTypes.SendAccess, CustomGrantTypes.WebAuthn], extensionGrantTypes.Order());
    }

    /// <summary>
    /// Self-hosted and cloud servers register different client providers, so both are checked.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClientProviders_AreInventoried(bool selfHosted)
    {
        using var factory = new IdentityApplicationFactory();
        factory.UpdateConfiguration("globalSettings:selfHosted", selfHosted.ToString());
        factory.UpdateConfiguration("globalSettings:internalIdentityKey", "internal-identity-key");
        factory.UpdateConfiguration("globalSettings:installation:id", Guid.NewGuid().ToString());
        factory.UpdateConfiguration("globalSettings:installation:key", "installation-key");
        var registeredProviders = new List<Type>();
        factory.ConfigureServices(services => registeredProviders.AddRange(services
            .Where(descriptor => descriptor.ServiceType == typeof(IClientProvider) && descriptor.IsKeyedService)
            .Select(descriptor => descriptor.KeyedImplementationType!)));

        _ = factory.Services;

        Assert.Subset(_clientProviders.Keys.ToHashSet(), registeredProviders.ToHashSet());
        Assert.Contains(typeof(UserClientProvider), registeredProviders);
        Assert.Equal(selfHosted, registeredProviders.Contains(typeof(InternalClientProvider)));
        Assert.Equal(!selfHosted, registeredProviders.Contains(typeof(InstallationClientProvider)));
    }

    /// <summary>
    /// The user API key client issues no refresh tokens, so its device claims only need to be present at login.
    /// </summary>
    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task UserClientProvider_ClientIssuesNoRefreshTokens(RegisterFinishRequestModel model)
    {
        model.UserAsymmetricKeys = new KeysRequestModel
        {
            AccountKeys = null,
            PublicKey = "public-key",
            EncryptedPrivateKey = "encrypted-private-key",
        };
        using var factory = new IdentityApplicationFactory();
        var user = await factory.RegisterNewIdentityFactoryUserAsync(model);

        using var scope = factory.Services.CreateScope();
        var client = await ActivatorUtilities.CreateInstance<UserClientProvider>(scope.ServiceProvider)
            .GetAsync(user.Id.ToString());

        Assert.NotNull(client);
        Assert.Equal([GrantType.ClientCredentials], client.AllowedGrantTypes);
        Assert.False(client.AllowOfflineAccess);
    }
}
