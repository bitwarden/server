using IdentityServer4.Stores;
using System.Threading.Tasks;
using IdentityServer4.Models;
using System.Collections.Generic;
using Bit.Core.Repositories;
using System;
using System.Security.Claims;
using IdentityModel;

namespace Bit.Core.IdentityServer
{
    public class ClientStore : IClientStore
    {
        private static IDictionary<string, Client> _apiClients = StaticClients.GetApiClients();

        private readonly IInstallationRepository _installationRepository;
        public ClientStore(
            IInstallationRepository installationRepository)
        {
            _installationRepository = installationRepository;
        }

        public async Task<Client> FindClientByIdAsync(string clientId)
        {
            if(clientId.StartsWith("installation."))
            {
                var idParts = clientId.Split('.');
                Guid id;
                if(idParts.Length > 1 && Guid.TryParse(idParts[1], out id))
                {
                    var installation = await _installationRepository.GetByIdAsync(id);
                    if(installation != null)
                    {
                        return new Client
                        {
                            ClientId = $"installation.{installation.Id}",
                            RequireClientSecret = true,
                            ClientSecrets = { new Secret(installation.Key.Sha256()) },
                            AllowedScopes = new string[] { "api.push" },
                            AllowedGrantTypes = GrantTypes.ClientCredentials,
                            AccessTokenLifetime = 3600 * 24,
                            Enabled = installation.Enabled,
                            Claims = new List<Claim> { new Claim(JwtClaimTypes.Subject, installation.Id.ToString()) }
                        };
                    }
                }
            }

            return _apiClients.ContainsKey(clientId) ? _apiClients[clientId] : null;
        }
    }
}
