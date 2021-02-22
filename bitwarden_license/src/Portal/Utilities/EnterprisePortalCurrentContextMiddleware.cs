using Bit.Core;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Bit.Portal.Utilities
{
    public class EnterprisePortalCurrentContextMiddleware
    {
        private readonly RequestDelegate _next;

        public EnterprisePortalCurrentContextMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext, EnterprisePortalCurrentContext currentContext,
            GlobalSettings globalSettings)
        {
            await currentContext.BuildAsync(httpContext, globalSettings);
            await _next.Invoke(httpContext);
        }
    }
}
