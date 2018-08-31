using System;
using System.Collections.Generic;
using System.Linq;
using Bit.Core.Models.Table;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Http;
using Bit.Core.Repositories;
using System.Threading.Tasks;
using System.Security.Claims;

namespace Bit.Core
{
    public class CurrentContext
    {
        private bool _builtHttpContext;
        private bool _builtClaimsPrincipal;
        private string _ip;

        public virtual HttpContext HttpContext { get; set; }
        public virtual Guid? UserId { get; set; }
        public virtual User User { get; set; }
        public virtual string DeviceIdentifier { get; set; }
        public virtual DeviceType? DeviceType { get; set; }
        public virtual string IpAddress => GetRequestIp();
        public virtual List<CurrentContentOrganization> Organizations { get; set; }
        public virtual Guid? InstallationId { get; set; }

        public void Build(HttpContext httpContext)
        {
            if(_builtHttpContext)
            {
                return;
            }

            _builtHttpContext = true;
            HttpContext = httpContext;
            Build(httpContext.User);

            if(DeviceIdentifier == null && httpContext.Request.Headers.ContainsKey("Device-Identifier"))
            {
                DeviceIdentifier = httpContext.Request.Headers["Device-Identifier"];
            }

            if(httpContext.Request.Headers.ContainsKey("Device-Type") &&
                Enum.TryParse(httpContext.Request.Headers["Device-Type"].ToString(), out DeviceType dType))
            {
                DeviceType = dType;
            }
        }

        public void Build(ClaimsPrincipal user)
        {
            if(_builtClaimsPrincipal)
            {
                return;
            }

            _builtClaimsPrincipal = true;
            if(user == null || !user.Claims.Any())
            {
                return;
            }

            var claimsDict = user.Claims.GroupBy(c => c.Type).ToDictionary(c => c.Key, c => c.Select(v => v));

            var subject = GetClaimValue(claimsDict, "sub");
            if(Guid.TryParse(subject, out var subIdGuid))
            {
                UserId = subIdGuid;
            }

            var clientId = GetClaimValue(claimsDict, "client_id");
            var clientSubject = GetClaimValue(claimsDict, "client_sub");
            if((clientId?.StartsWith("installation.") ?? false) && clientSubject != null)
            {
                if(Guid.TryParse(clientSubject, out var idGuid))
                {
                    InstallationId = idGuid;
                }
            }

            DeviceIdentifier = GetClaimValue(claimsDict, "device");

            Organizations = new List<CurrentContentOrganization>();
            if(claimsDict.ContainsKey("orgowner"))
            {
                Organizations.AddRange(claimsDict["orgowner"].Select(c =>
                    new CurrentContentOrganization
                    {
                        Id = new Guid(c.Value),
                        Type = OrganizationUserType.Owner
                    }));
            }

            if(claimsDict.ContainsKey("orgadmin"))
            {
                Organizations.AddRange(claimsDict["orgadmin"].Select(c =>
                    new CurrentContentOrganization
                    {
                        Id = new Guid(c.Value),
                        Type = OrganizationUserType.Admin
                    }));
            }

            if(claimsDict.ContainsKey("orguser"))
            {
                Organizations.AddRange(claimsDict["orguser"].Select(c =>
                    new CurrentContentOrganization
                    {
                        Id = new Guid(c.Value),
                        Type = OrganizationUserType.User
                    }));
            }
        }

        public bool OrganizationUser(Guid orgId)
        {
            return Organizations?.Any(o => o.Id == orgId) ?? false;
        }

        public bool OrganizationAdmin(Guid orgId)
        {
            return Organizations?.Any(o => o.Id == orgId &&
                (o.Type == OrganizationUserType.Owner || o.Type == OrganizationUserType.Admin)) ?? false;
        }

        public bool OrganizationOwner(Guid orgId)
        {
            return Organizations?.Any(o => o.Id == orgId && o.Type == OrganizationUserType.Owner) ?? false;
        }

        public async Task<ICollection<CurrentContentOrganization>> OrganizationMembershipAsync(
            IOrganizationUserRepository organizationUserRepository, Guid userId)
        {
            if(Organizations == null)
            {
                var userOrgs = await organizationUserRepository.GetManyByUserAsync(userId);
                Organizations = userOrgs.Where(ou => ou.Status == OrganizationUserStatusType.Confirmed)
                    .Select(ou => new CurrentContentOrganization(ou)).ToList();
            }
            return Organizations;
        }

        private string GetRequestIp()
        {
            if(!string.IsNullOrWhiteSpace(_ip))
            {
                return _ip;
            }

            if(HttpContext == null)
            {
                return null;
            }

            if(string.IsNullOrWhiteSpace(_ip))
            {
                _ip = HttpContext.Connection?.RemoteIpAddress?.ToString();
            }

            return _ip;
        }

        private string GetClaimValue(Dictionary<string, IEnumerable<Claim>> claims, string type)
        {
            if(!claims.ContainsKey(type))
            {
                return null;
            }

            return claims[type].FirstOrDefault()?.Value;
        }

        public class CurrentContentOrganization
        {
            public CurrentContentOrganization() { }

            public CurrentContentOrganization(OrganizationUser orgUser)
            {
                Id = orgUser.OrganizationId;
                Type = orgUser.Type;
            }

            public Guid Id { get; set; }
            public OrganizationUserType Type { get; set; }
        }
    }
}
