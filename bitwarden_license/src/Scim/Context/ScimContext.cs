using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.OrganizationConnectionConfigs;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.Scim.Context;

public class ScimContext : IScimContext
{
    private bool _builtHttpContext;

    // See IP list from Ping in docs: https://support.pingidentity.com/s/article/PingOne-IP-Addresses
    private static readonly HashSet<string> _pingIpAddresses =
    [
        "18.217.152.87",
        "52.14.10.143",
        "13.58.49.148",
        "34.211.92.81",
        "54.214.158.219",
        "34.218.98.164",
        "15.223.133.47",
        "3.97.84.38",
        "15.223.19.71",
        "3.97.98.120",
        "52.60.115.173",
        "3.97.202.223",
        "18.184.65.93",
        "52.57.244.92",
        "18.195.7.252",
        "108.128.67.71",
        "34.246.158.102",
        "108.128.250.27",
        "52.63.103.92",
        "13.54.131.18",
        "52.62.204.36"
    ];

    public ScimProviderType RequestScimProvider { get; set; } = ScimProviderType.Default;
    public ScimConfig ScimConfiguration { get; set; }
    public Guid? OrganizationId { get; set; }
    public Organization Organization { get; set; }

    public async virtual Task BuildAsync(
        HttpContext httpContext,
        GlobalSettings globalSettings,
        IOrganizationRepository organizationRepository,
        IOrganizationConnectionRepository organizationConnectionRepository)
    {
        if (_builtHttpContext)
        {
            return;
        }

        _builtHttpContext = true;

        string orgIdString = null;
        if (httpContext.Request.RouteValues.TryGetValue("organizationId", out var orgIdObject))
        {
            orgIdString = orgIdObject?.ToString();
        }

        if (Guid.TryParse(orgIdString, out var orgId))
        {
            OrganizationId = orgId;
            Organization = await organizationRepository.GetByIdAsync(orgId);
            if (Organization != null)
            {
                var scimConnections = await organizationConnectionRepository.GetByOrganizationIdTypeAsync(Organization.Id,
                    OrganizationConnectionType.Scim);
                ScimConfiguration = scimConnections?.FirstOrDefault()?.GetConfig<ScimConfig>();
            }
        }

        if (RequestScimProvider == ScimProviderType.Default &&
            httpContext.Request.Headers.TryGetValue("User-Agent", out var userAgent))
        {
            if (userAgent.ToString().StartsWith("Okta"))
            {
                RequestScimProvider = ScimProviderType.Okta;
            }
        }

        if (RequestScimProvider == ScimProviderType.Default &&
            httpContext.Request.Headers.ContainsKey("Adscimversion"))
        {
            RequestScimProvider = ScimProviderType.AzureAd;
        }

        var ipAddress = CoreHelpers.GetIpAddress(httpContext, globalSettings);
        if (RequestScimProvider == ScimProviderType.Default &&
            _pingIpAddresses.Contains(ipAddress))
        {
            RequestScimProvider = ScimProviderType.Ping;
        }
    }
}
