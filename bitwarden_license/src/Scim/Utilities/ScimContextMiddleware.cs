using System.Threading.Tasks;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Scim.Context;
using Microsoft.AspNetCore.Http;

namespace Bit.Scim.Utilities
{
    public class ScimContextMiddleware
    {
        private readonly RequestDelegate _next;

        public ScimContextMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext, IScimContext scimContext, GlobalSettings globalSettings,
            IOrganizationRepository organizationRepository)
        {
            await scimContext.BuildAsync(httpContext, globalSettings, organizationRepository);
            await _next.Invoke(httpContext);
        }
    }
}
