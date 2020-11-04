using IdentityServer4.Stores;
using System.Threading.Tasks;
using IdentityServer4.Models;
using System.Collections.Generic;
using Bit.Core.Repositories;
using System;
using IdentityModel;
using Bit.Core.Utilities;
using System.Security.Claims;

namespace Bit.Core.IdentityServer
{
    public class ClientStore : IClientStore
    {
        private readonly IInstallationRepository _installationRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUserRepository _userRepository;
        private readonly GlobalSettings _globalSettings;
        private readonly StaticClientStore _staticClientStore;

        public ClientStore(
            IInstallationRepository installationRepository,
            IOrganizationRepository organizationRepository,
            IUserRepository userRepository,
            GlobalSettings globalSettings,
            StaticClientStore staticClientStore)
        {
            _installationRepository = installationRepository;
            _organizationRepository = organizationRepository;
            _userRepository = userRepository;
            _globalSettings = globalSettings;
            _staticClientStore = staticClientStore;
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
                            AllowedScopes = new string[] { "api.push", "api.licensing" },
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
                        return new Client
                        {
                            ClientId = $"organization.{org.Id}",
                            RequireClientSecret = true,
                            ClientSecrets = { new Secret(org.ApiKey.Sha256()) },
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
            else if (clientId.Contains("user."))
            {
                var idParts = clientId.Split('.');
                if (idParts.Length > 2 && Guid.TryParse(idParts[2], out var id))
                {
                    var user = await _userRepository.GetByIdAsync(id);
                    if (user != null)
                    {
                        var cliUris = new List<string>();
                        for (var port = 8065; port <= 8070; port++)
                        {
                            cliUris.Add(string.Format("http://localhost:{0}", port));
                        }

                        Console.WriteLine(user.Id);
                        return new Client
                        {
                            ClientId = clientId,
                            RequireClientSecret = true,
                            ClientSecrets = { new Secret(user.ApiKey.Sha256()) },
                            AllowedScopes = new string[] { "api" },
                            AllowedGrantTypes = GrantTypes.ClientCredentials,
                            AccessTokenLifetime = 3600 * 1,
                            RedirectUris = cliUris,
                            PostLogoutRedirectUris = cliUris,
                            Claims = new List<ClientClaim>
                            {
                                new ClientClaim(JwtClaimTypes.Subject, user.Id.ToString()),
                                new ClientClaim(JwtClaimTypes.Email, user.Email),
                                new ClientClaim(JwtClaimTypes.EmailVerified, user.EmailVerified.ToString()),
                                new ClientClaim(JwtClaimTypes.Name, user.Name),
                                new ClientClaim(JwtClaimTypes.AuthenticationMethod, "Application", "external")
                            },
                        };
                    }
                }
            }

            return _staticClientStore.ApiClients.ContainsKey(clientId) ?
                _staticClientStore.ApiClients[clientId] : null;
        }
    }
}
