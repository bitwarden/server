using System;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Http;

namespace Bit.Scim.Context
{
    public class ScimContext : IScimContext
    {
        private bool _builtHttpContext;

        public virtual HttpContext HttpContext { get; set; }
        public ScimProviderType? ScimProvider { get; set; }
        public Guid? OrganizationId { get; set; }
        public Organization Organization { get; set; }

        public async virtual Task BuildAsync(
            HttpContext httpContext,
            GlobalSettings globalSettings,
            IOrganizationRepository organizationRepository)
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
            }

            if (ScimProvider == null && httpContext.Request.Headers.ContainsKey("User-Agent"))
            {
                var userAgent = httpContext.Request.Headers["User-Agent"].ToString();
                if (userAgent.StartsWith("Okta"))
                {
                    ScimProvider = ScimProviderType.Okta;
                }
            }
            if (ScimProvider == null && httpContext.Request.Headers.ContainsKey("Adscimversion"))
            {
                ScimProvider = ScimProviderType.AzureAd;
            }
        }
    }
}
