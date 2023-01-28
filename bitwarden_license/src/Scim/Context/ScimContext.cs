using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.OrganizationConnectionConfigs;
using Bit.Core.Repositories;
using Bit.Core.Settings;

namespace Bit.Scim.Context;

public class ScimContext : IScimContext
{
    private bool _builtHttpContext;

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
    }
}
