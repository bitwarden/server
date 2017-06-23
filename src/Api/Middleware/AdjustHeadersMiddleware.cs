using Bit.Core;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;

namespace Bit.Api.Middleware
{
    public class AdjustHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public AdjustHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext, CurrentContext currentContext)
        {
            httpContext.Response.OnStarting((state) =>
            {
                if(httpContext.Response.Headers.Count > 0 && httpContext.Response.Headers.ContainsKey("Content-Type"))
                {
                    var contentType = httpContext.Response.Headers["Content-Type"].ToString();
                    if(contentType.StartsWith("application/fido.trusted-apps+json"))
                    {
                        httpContext.Response.Headers.Remove("Content-Type");
                        httpContext.Response.Headers.Append("Content-Type", "application/fido.trusted-apps+json");
                    }
                }

                return Task.FromResult(0);
            }, null);


            await _next.Invoke(httpContext);
        }
    }
}
