using Bit.Core.Enums;
using Bit.Core.IdentityServer;
using Bit.Core.Settings;
using Bit.Identity.IdentityServer.Enums;
using Duende.IdentityServer.Models;

namespace Bit.Identity.IdentityServer.StaticClients;
public static class SendClientBuilder
{
    public static Client Build(GlobalSettings globalSettings)
    {
        return new Client()
        {
            ClientId = BitwardenClient.Send,
            AllowedGrantTypes = [CustomGrantTypes.SendAccess],
            AccessTokenLifetime = 60 * globalSettings.SendAccessTokenLifetimeInMinutes,

            // Do not allow refresh tokens to be issued.
            AllowOfflineAccess = false,

            // Send is a public anonymous client, so no secret is required (or really possible to use securely).
            RequireClientSecret = false,

            // Allow web vault to use this client.
            AllowedCorsOrigins = [globalSettings.BaseServiceUri.Vault],

            // Setup API scopes that the client can request in the scope property of the token request.
            AllowedScopes = [ApiScopes.Send],
        };
    }
}
