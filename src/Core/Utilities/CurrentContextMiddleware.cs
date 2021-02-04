using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Bit.Core.Context;

namespace Bit.Core.Utilities
{
    public class CurrentContextMiddleware
    {
        private readonly RequestDelegate _next;

        public CurrentContextMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext, CurrentContext currentContext, GlobalSettings globalSettings)
        {
            await currentContext.BuildAsync(httpContext, globalSettings);
            await _next.Invoke(httpContext);
        }
    }
}
