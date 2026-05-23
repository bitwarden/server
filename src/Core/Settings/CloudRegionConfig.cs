using Bit.Core.Enums;

namespace Bit.Core.Settings;

public sealed class CloudRegionConfig
{
    private CloudRegionConfig(
        CloudRegion region,
        string domain,
        string apiUrl,
        string identityUrl,
        string vaultUrl,
        string notificationsUrl,
        string iconsUrl,
        string eventsUrl,
        string ssoCallbackUri)
    {
        Region = region;
        Domain = domain;
        ApiUrl = apiUrl;
        IdentityUrl = identityUrl;
        VaultUrl = vaultUrl;
        NotificationsUrl = notificationsUrl;
        IconsUrl = iconsUrl;
        EventsUrl = eventsUrl;
        SsoCallbackUri = ssoCallbackUri;
    }

    public CloudRegion Region { get; }
    public string Domain { get; }
    public string ApiUrl { get; }
    public string IdentityUrl { get; }
    public string VaultUrl { get; }
    public string NotificationsUrl { get; }
    public string IconsUrl { get; }
    public string EventsUrl { get; }
    public string SsoCallbackUri { get; }

    public static readonly CloudRegionConfig[] All =
    [
        new(
            CloudRegion.US,
            "bitwarden.com",
            "https://api.bitwarden.com",
            "https://identity.bitwarden.com",
            "https://vault.bitwarden.com",
            "https://notifications.bitwarden.com",
            "https://icons.bitwarden.com",
            "https://events.bitwarden.com",
            "https://bitwarden.com/sso-callback"),
        new(
            CloudRegion.EU,
            "bitwarden.eu",
            "https://api.bitwarden.eu",
            "https://identity.bitwarden.eu",
            "https://vault.bitwarden.eu",
            "https://notifications.bitwarden.eu",
            "https://icons.bitwarden.eu",
            "https://events.bitwarden.eu",
            "https://bitwarden.eu/sso-callback"),
    ];

    public static CloudRegionConfig FindByDomain(string domain) =>
        All.FirstOrDefault(x => x.Domain == domain) ?? All[0];

    public static CloudRegionConfig FindByRegion(CloudRegion region) =>
        All.FirstOrDefault(x => x.Region == region) ?? All[0];
}
