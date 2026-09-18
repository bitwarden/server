using System.Collections.Frozen;
using Bit.Core;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Identity.IdentityServer.StaticClients;
using Duende.IdentityServer.Models;

namespace Bit.Identity.IdentityServer;

public class StaticClientStore
{
    public StaticClientStore(GlobalSettings globalSettings)
    {
        Clients = new List<Client>
        {
            new ApiClient(BuildConfig(globalSettings, BitwardenClient.Mobile, 60, 3600)),
            new ApiClient(BuildConfig(globalSettings, BitwardenClient.Web, 7, 3600)),
            new ApiClient(BuildConfig(globalSettings, BitwardenClient.Browser, 30, 3600)),
            new ApiClient(BuildConfig(globalSettings, BitwardenClient.Desktop, 30, 3600)),
            new ApiClient(BuildConfig(globalSettings, BitwardenClient.Cli, 30, 3600)),
            new ApiClient(BuildConfig(globalSettings, BitwardenClient.DirectoryConnector, 30, 24 * 3600)),
            SendClientBuilder.Build(globalSettings),
        }.ToFrozenDictionary(c => c.ClientId);
    }

    public FrozenDictionary<string, Client> Clients { get; }

    private static ApiClientConfiguration BuildConfig(
        GlobalSettings globalSettings, string id, int refreshTokenSlidingDays, int accessTokenLifetimeSeconds)
    {
        var identityServer = globalSettings.IdentityServer;
        var vaultUri = globalSettings.BaseServiceUri.Vault;

        var config = new ApiClientConfiguration
        {
            Id = id,
            RefreshTokenSlidingDays = refreshTokenSlidingDays,
            AccessTokenLifetimeSeconds = accessTokenLifetimeSeconds,
            ApplyAbsoluteExpirationOnRefreshToken = identityServer.ApplyAbsoluteExpirationOnRefreshToken,
            SlidingRefreshTokenLifetimeSecondsOverride = identityServer.SlidingRefreshTokenLifetimeSeconds,
            AbsoluteRefreshTokenLifetimeSeconds = identityServer.AbsoluteRefreshTokenLifetimeSeconds,
        };

        return id switch
        {
            BitwardenClient.Web or BitwardenClient.Browser => config with
            {
                RedirectUris = [$"{vaultUri}/sso-connector.html"],
                PostLogoutRedirectUris = [vaultUri],
                AllowedCorsOrigins = [vaultUri],
            },
            BitwardenClient.Desktop => config with
            {
                RedirectUris = ["bitwarden://sso-callback", .. LocalhostPortUris()],
                PostLogoutRedirectUris = ["bitwarden://logged-out"],
            },
            BitwardenClient.DirectoryConnector => config with
            {
                RedirectUris = [.. LocalhostPortUris(), "bwdc://sso-callback"],
                PostLogoutRedirectUris = [.. LocalhostPortUris(), "bwdc://logged-out"],
            },
            BitwardenClient.Cli => config with
            {
                RedirectUris = [.. LocalhostPortUris()],
                PostLogoutRedirectUris = [.. LocalhostPortUris()],
            },
            BitwardenClient.Mobile => config with
            {
                RedirectUris = [.. Constants.BitwardenMobileSsoCallbackUris],
                PostLogoutRedirectUris = ["bitwarden://logged-out"],
            },
            _ => config,
        };
    }

    private static IEnumerable<string> LocalhostPortUris() =>
        Enumerable.Range(8065, 6).Select(p => $"http://localhost:{p}");
}
