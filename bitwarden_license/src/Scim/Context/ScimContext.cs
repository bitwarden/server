using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.OrganizationConnectionConfigs;
using Bit.Core.Repositories;
using Bit.Core.Settings;

namespace Bit.Scim.Context
{
    public class ScimContext : IScimContext
    {
        private bool _builtHttpContext;

        public virtual HttpContext HttpContext { get; set; }
        public ScimProviderType? RequestScimProvider { get; set; }
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
            HttpContext = httpContext;

            string orgIdString = null;
            if (httpContext.Request.RouteValues.TryGetValue("organizationId", out var orgIdObject))
            {
                orgIdString = orgIdObject?.ToString();
            }

            if (!string.IsNullOrWhiteSpace(orgIdString) && Guid.TryParse(orgIdString, out var orgId))
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

            if (RequestScimProvider == null && httpContext.Request.Headers.ContainsKey("User-Agent"))
            {
                var userAgent = httpContext.Request.Headers["User-Agent"].ToString();
                if (userAgent.StartsWith("Okta"))
                {
                    RequestScimProvider = ScimProviderType.Okta;
                }
            }
            if (RequestScimProvider == null && httpContext.Request.Headers.ContainsKey("Adscimversion"))
            {
                RequestScimProvider = ScimProviderType.AzureAd;
            }
        }
    }
}
