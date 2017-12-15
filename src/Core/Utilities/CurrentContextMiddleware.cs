using Bit.Core.Enums;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Bit.Core.Utilities
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
            currentContext.HttpContext = httpContext;

            if(httpContext.User != null && httpContext.User.Claims.Any())
            {
                var claimsDict = httpContext.User.Claims
                    .GroupBy(c => c.Type)
                    .ToDictionary(c => c.Key, c => c.Select(v => v));

                var subject = GetClaimValue(claimsDict, "sub");
                if(Guid.TryParse(subject, out var subIdGuid))
                {
                    currentContext.UserId = subIdGuid;
                }

                var clientId = GetClaimValue(claimsDict, "client_id");
                var clientSubject = GetClaimValue(claimsDict, "client_sub");
                if((clientId?.StartsWith("installation.") ?? false) && clientSubject != null)
                {
                    if(Guid.TryParse(clientSubject, out var idGuid))
                    {
                        currentContext.InstallationId = idGuid;
                    }
                }

                currentContext.DeviceIdentifier = GetClaimValue(claimsDict, "device");

                if(claimsDict.ContainsKey("orgowner"))
                {
                    currentContext.Organizations.AddRange(claimsDict["orgowner"].Select(c =>
                        new CurrentContext.CurrentContentOrganization
                        {
                            Id = new Guid(c.Value),
                            Type = OrganizationUserType.Owner
                        }));
                }

                if(claimsDict.ContainsKey("orgadmin"))
                {
                    currentContext.Organizations.AddRange(claimsDict["orgadmin"].Select(c =>
                        new CurrentContext.CurrentContentOrganization
                        {
                            Id = new Guid(c.Value),
                            Type = OrganizationUserType.Admin
                        }));
                }

                if(claimsDict.ContainsKey("orguser"))
                {
                    currentContext.Organizations.AddRange(claimsDict["orguser"].Select(c =>
                        new CurrentContext.CurrentContentOrganization
                        {
                            Id = new Guid(c.Value),
                            Type = OrganizationUserType.User
                        }));
                }
            }

            if(currentContext.DeviceIdentifier == null && httpContext.Request.Headers.ContainsKey("Device-Identifier"))
            {
                currentContext.DeviceIdentifier = httpContext.Request.Headers["Device-Identifier"];
            }

            if(httpContext.Request.Headers.ContainsKey("Device-Type") &&
                Enum.TryParse(httpContext.Request.Headers["Device-Type"].ToString(), out DeviceType dType))
            {
                currentContext.DeviceType = dType;
            }

            await _next.Invoke(httpContext);
        }

        private string GetClaimValue(Dictionary<string, IEnumerable<Claim>> claims, string type)
        {
            if(!claims.ContainsKey(type))
            {
                return null;
            }

            return claims[type].FirstOrDefault()?.Value;
        }
    }
}
