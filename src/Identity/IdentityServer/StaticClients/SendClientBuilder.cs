using Bit.Core.Enums;
using Bit.Core.IdentityServer;
using Bit.Core.Settings;
using Bit.Identity.IdentityServer.RequestValidators;
using Duende.IdentityServer.Models;

namespace Bit.Identity.IdentityServer.StaticClients;
public static class SendClientBuilder
{
    public static Client Build(GlobalSettings globalSettings)
    {
        return new Client()
        {
            ClientId = BitwardenClient.Send,
            AllowedGrantTypes = new[] { SendAccessGrantValidator.GrantType },
            AccessTokenLifetime = 60 * 5, // 5 minutes

            // Do not allow refresh tokens to be issued.
            AllowOfflineAccess = false,

            // Send is a public anonymous client, so no secret is required (or really possible to use securely).
            RequireClientSecret = false,

            // Allow web vault to use this client.
            AllowedCorsOrigins = new[] { globalSettings.BaseServiceUri.Vault },

            // Setup API scopes that the client can request in the scope property of the token request.
            AllowedScopes = new string[] { ApiScopes.Send },
        };
    }
}
