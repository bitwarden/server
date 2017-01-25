using Bit.Core;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;

namespace Bit.Api.Middleware
{
    public class CurrentContextMiddleware
    {
        private readonly RequestDelegate _next;

        public CurrentContextMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext, CurrentContext currentContext)
        {
            if(httpContext.User != null)
            {
                var securityStampClaim = httpContext.User.Claims.FirstOrDefault(c => c.Type == "device");
                currentContext.DeviceIdentifier = securityStampClaim?.Value;
            }

            if(currentContext.DeviceIdentifier == null && httpContext.Request.Headers.ContainsKey("Device-Identifier"))
            {
                currentContext.DeviceIdentifier = httpContext.Request.Headers["Device-Identifier"];
            }

            await _next.Invoke(httpContext);
        }
    }
}
