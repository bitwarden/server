using System.Threading.Tasks;
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

        public async Task Invoke(HttpContext httpContext, IScimContext scimContext, GlobalSettings globalSettings)
        {
            await scimContext.BuildAsync(httpContext, globalSettings);
            await _next.Invoke(httpContext);
        }
    }
}
