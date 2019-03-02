using IdentityServer4.Stores;
using System.Threading.Tasks;
using IdentityServer4.Models;
using System.Collections.Generic;
using Bit.Core.Repositories;
using System;
using System.Security.Claims;
using IdentityModel;
using Bit.Core.Utilities;

namespace Bit.Core.IdentityServer
{
    public class ClientStore : IClientStore
    {
        private static IDictionary<string, Client> _apiClients = StaticClients.GetApiClients();

        private readonly IInstallationRepository _installationRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly GlobalSettings _globalSettings;

        public ClientStore(
            IInstallationRepository installationRepository,
            IOrganizationRepository organizationRepository,
            GlobalSettings globalSettings)
        {
            _installationRepository = installationRepository;
            _organizationRepository = organizationRepository;
            _globalSettings = globalSettings;
        }

        public async Task<Client> FindClientByIdAsync(string clientId)
        {
            if(!_globalSettings.SelfHosted && clientId.StartsWith("installation."))
            {
                var idParts = clientId.Split('.');
                if(idParts.Length > 1 && Guid.TryParse(idParts[1], out Guid id))
                {
                    var installation = await _installationRepository.GetByIdAsync(id);
                    if(installation != null)
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
                            Claims = new List<Claim> { new Claim(JwtClaimTypes.Subject, installation.Id.ToString()) }
                        };
                    }
                }
            }
            else if(_globalSettings.SelfHosted && clientId.StartsWith("internal.") &&
                CoreHelpers.SettingHasValue(_globalSettings.InternalIdentityKey))
            {
                var idParts = clientId.Split('.');
                if(idParts.Length > 1)
                {
                    var id = idParts[1];
                    if(!string.IsNullOrWhiteSpace(id))
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
                            Claims = new List<Claim> { new Claim(JwtClaimTypes.Subject, id) }
                        };
                    }
                }
            }
            else if(clientId.StartsWith("organization."))
            {
                var idParts = clientId.Split('.');
                if(idParts.Length > 1 && Guid.TryParse(idParts[1], out var id))
                {
                    var org = await _organizationRepository.GetByIdAsync(id);
                    if(org != null)
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
                            Claims = new List<Claim> { new Claim(JwtClaimTypes.Subject, org.Id.ToString()) }
                        };
                    }
                }
            }

            return _apiClients.ContainsKey(clientId) ? _apiClients[clientId] : null;
        }
    }
}
