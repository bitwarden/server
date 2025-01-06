using System.Collections.ObjectModel;
using System.Security.Claims;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Identity;
using Bit.Core.IdentityServer;
using Bit.Core.Platform.Installations;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using IdentityModel;

namespace Bit.Identity.IdentityServer;

public class ClientStore : IClientStore
{
    private readonly IInstallationRepository _installationRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly GlobalSettings _globalSettings;
    private readonly StaticClientStore _staticClientStore;
    private readonly ILicensingService _licensingService;
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IProviderUserRepository _providerUserRepository;
    private readonly IOrganizationApiKeyRepository _organizationApiKeyRepository;
    private readonly IApiKeyRepository _apiKeyRepository;

    public ClientStore(
        IInstallationRepository installationRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        GlobalSettings globalSettings,
        StaticClientStore staticClientStore,
        ILicensingService licensingService,
        ICurrentContext currentContext,
        IOrganizationUserRepository organizationUserRepository,
        IProviderUserRepository providerUserRepository,
        IOrganizationApiKeyRepository organizationApiKeyRepository,
        IApiKeyRepository apiKeyRepository)
    {
        _installationRepository = installationRepository;
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _globalSettings = globalSettings;
        _staticClientStore = staticClientStore;
        _licensingService = licensingService;
        _currentContext = currentContext;
        _organizationUserRepository = organizationUserRepository;
        _providerUserRepository = providerUserRepository;
        _organizationApiKeyRepository = organizationApiKeyRepository;
        _apiKeyRepository = apiKeyRepository;
    }

    public async Task<Client> FindClientByIdAsync(string clientId)
    {
        if (!_globalSettings.SelfHosted && clientId.StartsWith("installation."))
        {
            return await CreateInstallationClientAsync(clientId);
        }

        if (_globalSettings.SelfHosted && clientId.StartsWith("internal.") &&
            CoreHelpers.SettingHasValue(_globalSettings.InternalIdentityKey))
        {
            return CreateInternalClient(clientId);
        }

        if (clientId.StartsWith("organization."))
        {
            return await CreateOrganizationClientAsync(clientId);
        }

        if (clientId.StartsWith("user."))
        {
            return await CreateUserClientAsync(clientId);
        }

        if (_staticClientStore.ApiClients.TryGetValue(clientId, out var client))
        {
            return client;
        }

        return await CreateApiKeyClientAsync(clientId);
    }

    private async Task<Client> CreateApiKeyClientAsync(string clientId)
    {
        if (!Guid.TryParse(clientId, out var guid))
        {
            return null;
        }

        var apiKey = await _apiKeyRepository.GetDetailsByIdAsync(guid);

        if (apiKey == null || apiKey.ExpireAt <= DateTime.Now)
        {
            return null;
        }

        switch (apiKey)
        {
            case ServiceAccountApiKeyDetails key:
                var org = await _organizationRepository.GetByIdAsync(key.ServiceAccountOrganizationId);
                if (!org.UseSecretsManager || !org.Enabled)
                {
                    return null;
                }
                break;
        }

        var client = new Client
        {
            ClientId = clientId,
            RequireClientSecret = true,
            ClientSecrets = { new Secret(apiKey.ClientSecretHash) },
            AllowedScopes = apiKey.GetScopes(),
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AccessTokenLifetime = 3600 * 1,
            ClientClaimsPrefix = null,
            Properties = new Dictionary<string, string> {
                {"encryptedPayload", apiKey.EncryptedPayload},
            },
            Claims = new List<ClientClaim>
            {
                new(JwtClaimTypes.Subject, apiKey.ServiceAccountId.ToString()),
                new(Claims.Type, IdentityClientType.ServiceAccount.ToString()),
            },
        };

        switch (apiKey)
        {
            case ServiceAccountApiKeyDetails key:
                client.Claims.Add(new ClientClaim(Claims.Organization, key.ServiceAccountOrganizationId.ToString()));
                break;
        }

        return client;
    }

    private async Task<Client> CreateUserClientAsync(string clientId)
    {
        var idParts = clientId.Split('.');
        if (idParts.Length <= 1 || !Guid.TryParse(idParts[1], out var id))
        {
            return null;
        }

        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            return null;
        }

        var claims = new Collection<ClientClaim>
        {
            new(JwtClaimTypes.Subject, user.Id.ToString()),
            new(JwtClaimTypes.AuthenticationMethod, "Application", "external"),
            new(Claims.Type, IdentityClientType.User.ToString()),
        };
        var orgs = await _currentContext.OrganizationMembershipAsync(_organizationUserRepository, user.Id);
        var providers = await _currentContext.ProviderMembershipAsync(_providerUserRepository, user.Id);
        var isPremium = await _licensingService.ValidateUserPremiumAsync(user);
        foreach (var claim in CoreHelpers.BuildIdentityClaims(user, orgs, providers, isPremium))
        {
            var upperValue = claim.Value.ToUpperInvariant();
            var isBool = upperValue is "TRUE" or "FALSE";
            claims.Add(isBool
                ? new ClientClaim(claim.Key, claim.Value, ClaimValueTypes.Boolean)
                : new ClientClaim(claim.Key, claim.Value)
            );
        }

        return new Client
        {
            ClientId = clientId,
            RequireClientSecret = true,
            ClientSecrets = { new Secret(user.ApiKey.Sha256()) },
            AllowedScopes = new[] { "api" },
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AccessTokenLifetime = 3600 * 1,
            ClientClaimsPrefix = null,
            Claims = claims,
        };
    }

    private async Task<Client> CreateOrganizationClientAsync(string clientId)
    {
        var idParts = clientId.Split('.');
        if (idParts.Length <= 1 || !Guid.TryParse(idParts[1], out var id))
        {
            return null;
        }

        var org = await _organizationRepository.GetByIdAsync(id);
        if (org == null)
        {
            return null;
        }

        var orgApiKey = (await _organizationApiKeyRepository
            .GetManyByOrganizationIdTypeAsync(org.Id, OrganizationApiKeyType.Default))
            .First();

        return new Client
        {
            ClientId = $"organization.{org.Id}",
            RequireClientSecret = true,
            ClientSecrets = { new Secret(orgApiKey.ApiKey.Sha256()) },
            AllowedScopes = new[] { ApiScopes.ApiOrganization },
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AccessTokenLifetime = 3600 * 1,
            Enabled = org.Enabled && org.UseApi,
            Claims = new List<ClientClaim>
            {
                new(JwtClaimTypes.Subject, org.Id.ToString()),
                new(Claims.Type, IdentityClientType.Organization.ToString()),
            },
        };
    }

    private Client CreateInternalClient(string clientId)
    {
        var idParts = clientId.Split('.');
        if (idParts.Length <= 1)
        {
            return null;
        }

        var id = idParts[1];
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return new Client
        {
            ClientId = $"internal.{id}",
            RequireClientSecret = true,
            ClientSecrets = { new Secret(_globalSettings.InternalIdentityKey.Sha256()) },
            AllowedScopes = new[] { ApiScopes.Internal },
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AccessTokenLifetime = 3600 * 24,
            Enabled = true,
            Claims = new List<ClientClaim>
            {
                new(JwtClaimTypes.Subject, id),
            },
        };
    }

    private async Task<Client> CreateInstallationClientAsync(string clientId)
    {
        var idParts = clientId.Split('.');
        if (idParts.Length <= 1 || !Guid.TryParse(idParts[1], out Guid id))
        {
            return null;
        }

        var installation = await _installationRepository.GetByIdAsync(id);
        if (installation == null)
        {
            return null;
        }

        return new Client
        {
            ClientId = $"installation.{installation.Id}",
            RequireClientSecret = true,
            ClientSecrets = { new Secret(installation.Key.Sha256()) },
            AllowedScopes = new[]
            {
                ApiScopes.ApiPush,
                ApiScopes.ApiLicensing,
                ApiScopes.ApiInstallation,
            },
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AccessTokenLifetime = 3600 * 24,
            Enabled = installation.Enabled,
            Claims = new List<ClientClaim>
            {
                new(JwtClaimTypes.Subject, installation.Id.ToString()),
            },
        };
    }
}
