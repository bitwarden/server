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

                var orgOwnerClaims = httpContext.User.Claims.Where(c => c.Type == "orgowner");
                if(orgOwnerClaims.Any())
                {
                    currentContext.Organizations.AddRange(orgOwnerClaims.Select(c =>
                        new CurrentContext.CurrentContentOrganization
                        {
                            Id = new System.Guid(c.Value),
                            Type = Core.Enums.OrganizationUserType.Owner
                        }));
                }

                var orgAdminClaims = httpContext.User.Claims.Where(c => c.Type == "orgadmin");
                if(orgAdminClaims.Any())
                {
                    currentContext.Organizations.AddRange(orgAdminClaims.Select(c =>
                        new CurrentContext.CurrentContentOrganization
                        {
                            Id = new System.Guid(c.Value),
                            Type = Core.Enums.OrganizationUserType.Admin
                        }));
                }

                var orgUserClaims = httpContext.User.Claims.Where(c => c.Type == "orguser");
                if(orgUserClaims.Any())
                {
                    currentContext.Organizations.AddRange(orgUserClaims.Select(c =>
                        new CurrentContext.CurrentContentOrganization
                        {
                            Id = new System.Guid(c.Value),
                            Type = Core.Enums.OrganizationUserType.User
                        }));
                }
            }

            if(currentContext.DeviceIdentifier == null && httpContext.Request.Headers.ContainsKey("Device-Identifier"))
            {
                currentContext.DeviceIdentifier = httpContext.Request.Headers["Device-Identifier"];
            }

            await _next.Invoke(httpContext);
        }
    }
}
