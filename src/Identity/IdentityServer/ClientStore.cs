using System.Collections.ObjectModel;
using System.Security.Claims;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using IdentityModel;
using IdentityServer4.Models;
using IdentityServer4.Stores;

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
    private readonly IProviderOrganizationRepository _providerOrganizationRepository;
    private readonly IOrganizationApiKeyRepository _organizationApiKeyRepository;

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
        IProviderOrganizationRepository providerOrganizationRepository,
        IOrganizationApiKeyRepository organizationApiKeyRepository)
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
        _providerOrganizationRepository = providerOrganizationRepository;
        _organizationApiKeyRepository = organizationApiKeyRepository;
    }

    public async Task<Client> FindClientByIdAsync(string clientId)
    {
        if (!_globalSettings.SelfHosted && clientId.StartsWith("installation."))
        {
            var idParts = clientId.Split('.');
            if (idParts.Length > 1 && Guid.TryParse(idParts[1], out Guid id))
            {
                var installation = await _installationRepository.GetByIdAsync(id);
                if (installation != null)
                {
                    return new Client
                    {
                        ClientId = $"installation.{installation.Id}",
                        RequireClientSecret = true,
                        ClientSecrets = { new Secret(installation.Key.Sha256()) },
                        AllowedScopes = new string[] { "api.push", "api.licensing", "api.installation" },
                        AllowedGrantTypes = GrantTypes.ClientCredentials,
                        AccessTokenLifetime = 3600 * 24,
                        Enabled = installation.Enabled,
                        Claims = new List<ClientClaim>
                        {
                            new ClientClaim(JwtClaimTypes.Subject, installation.Id.ToString())
                        }
                    };
                }
            }
        }
        else if (_globalSettings.SelfHosted && clientId.StartsWith("internal.") &&
            CoreHelpers.SettingHasValue(_globalSettings.InternalIdentityKey))
        {
            var idParts = clientId.Split('.');
            if (idParts.Length > 1)
            {
                var id = idParts[1];
                if (!string.IsNullOrWhiteSpace(id))
                {
                    return new Client
                    {
                        ClientId = $"internal.{id}",
                        RequireClientSecret = true,
                        ClientSecrets = { new Secret(_globalSettings.InternalIdentityKey.Sha256()) },
                        AllowedScopes = new string[] { "internal" },
                        AllowedGrantTypes = GrantTypes.ClientCredentials,
                        AccessTokenLifetime = 3600 * 24,
                        Enabled = true,
                        Claims = new List<ClientClaim>
                        {
                            new ClientClaim(JwtClaimTypes.Subject, id)
                        }
                    };
                }
            }
        }
        else if (clientId.StartsWith("organization."))
        {
            var idParts = clientId.Split('.');
            if (idParts.Length > 1 && Guid.TryParse(idParts[1], out var id))
            {
                var org = await _organizationRepository.GetByIdAsync(id);
                if (org != null)
                {
                    var orgApiKey = (await _organizationApiKeyRepository
                        .GetManyByOrganizationIdTypeAsync(org.Id, OrganizationApiKeyType.Default))
                        .First();
                    return new Client
                    {
                        ClientId = $"organization.{org.Id}",
                        RequireClientSecret = true,
                        ClientSecrets = { new Secret(orgApiKey.ApiKey.Sha256()) },
                        AllowedScopes = new string[] { "api.organization" },
                        AllowedGrantTypes = GrantTypes.ClientCredentials,
                        AccessTokenLifetime = 3600 * 1,
                        Enabled = org.Enabled && org.UseApi,
                        Claims = new List<ClientClaim>
                        {
                            new ClientClaim(JwtClaimTypes.Subject, org.Id.ToString())
                        }
                    };
                }
            }
        }
        else if (clientId.StartsWith("user."))
        {
            var idParts = clientId.Split('.');
            if (idParts.Length > 1 && Guid.TryParse(idParts[1], out var id))
            {
                var user = await _userRepository.GetByIdAsync(id);
                if (user != null)
                {
                    var claims = new Collection<ClientClaim>()
                    {
                        new ClientClaim(JwtClaimTypes.Subject, user.Id.ToString()),
                        new ClientClaim(JwtClaimTypes.AuthenticationMethod, "Application", "external")
                    };
                    var orgs = await _currentContext.OrganizationMembershipAsync(_organizationUserRepository, user.Id);
                    var providers = await _currentContext.ProviderMembershipAsync(_providerUserRepository, user.Id);
                    var isPremium = await _licensingService.ValidateUserPremiumAsync(user);
                    foreach (var claim in CoreHelpers.BuildIdentityClaims(user, orgs, providers, isPremium))
                    {
                        var upperValue = claim.Value.ToUpperInvariant();
                        var isBool = upperValue == "TRUE" || upperValue == "FALSE";
                        claims.Add(isBool ?
                            new ClientClaim(claim.Key, claim.Value, ClaimValueTypes.Boolean) :
                            new ClientClaim(claim.Key, claim.Value)
                        );
                    }

                    return new Client
                    {
                        ClientId = clientId,
                        RequireClientSecret = true,
                        ClientSecrets = { new Secret(user.ApiKey.Sha256()) },
                        AllowedScopes = new string[] { "api" },
                        AllowedGrantTypes = GrantTypes.ClientCredentials,
                        AccessTokenLifetime = 3600 * 1,
                        ClientClaimsPrefix = null,
                        Claims = claims
                    };
                }
            }
        }

        return _staticClientStore.ApiClients.ContainsKey(clientId) ?
            _staticClientStore.ApiClients[clientId] : null;
    }
}
