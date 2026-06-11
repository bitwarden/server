using Bit.Core.Enums;

namespace Bit.Core.Settings;

/// <summary>
/// Canonical per-region URLs for Bitwarden's public cloud regions. A region is one
/// entry in <see cref="All"/>; consumers resolve URLs through
/// <see cref="FindByRegion"/> or <see cref="FindByDomain"/>.
/// </summary>
/// <remarks>
/// Scope: URLs that are fixed by region and shared across multiple consumers belong
/// here. URLs that vary per deployment -- those sourced from <c>GlobalSettings</c>/
/// appsettings, such as notifications, icons, and events -- do not.
/// </remarks>
public sealed class CloudRegionConfig
{
    private CloudRegionConfig(
        CloudRegion region,
        string domain,
        string apiUrl,
        string identityUrl,
        string vaultUrl,
        string ssoCallbackUri)
    {
        Region = region;
        Domain = domain;
        ApiUrl = apiUrl;
        IdentityUrl = identityUrl;
        VaultUrl = vaultUrl;
        SsoCallbackUri = ssoCallbackUri;
    }

    public CloudRegion Region { get; }
    public string Domain { get; }
    public string ApiUrl { get; }
    public string IdentityUrl { get; }
    public string VaultUrl { get; }
    public string SsoCallbackUri { get; }

    public static readonly CloudRegionConfig[] All =
    [
        new(
            CloudRegion.US,
            "bitwarden.com",
            "https://api.bitwarden.com",
            "https://identity.bitwarden.com",
            "https://vault.bitwarden.com",
            "https://bitwarden.com/sso-callback"),
        new(
            CloudRegion.EU,
            "bitwarden.eu",
            "https://api.bitwarden.eu",
            "https://identity.bitwarden.eu",
            "https://vault.bitwarden.eu",
            "https://bitwarden.eu/sso-callback"),
        new(
            CloudRegion.Gov,
            "bitwarden-gov.com",
            "https://api.bitwarden-gov.com",
            "https://identity.bitwarden-gov.com",
            "https://vault.bitwarden-gov.com",
            "https://bitwarden-gov.com/sso-callback"),
    ];

    public static CloudRegionConfig FindByDomain(string domain) =>
        All.FirstOrDefault(x => x.Domain == domain) ?? All[0];

    public static CloudRegionConfig FindByRegion(CloudRegion region) =>
        All.FirstOrDefault(x => x.Region == region) ?? All[0];
}
