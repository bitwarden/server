using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Http;

namespace Bit.Scim.Context
{
    public class ScimContext : IScimContext
    {
        private bool _builtHttpContext;

        public virtual HttpContext HttpContext { get; set; }
        public ScimProviderType? ScimProvider { get; set; }

        public virtual Task BuildAsync(HttpContext httpContext, GlobalSettings globalSettings)
        {
            if (_builtHttpContext)
            {
                return Task.FromResult(0);
            }

            _builtHttpContext = true;
            HttpContext = httpContext;

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

            return Task.FromResult(0);
        }
    }
}
