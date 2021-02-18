using System;
using System.Collections.Generic;
using System.Linq;
using Bit.Core.Models.Table;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Http;
using Bit.Core.Repositories;
using System.Threading.Tasks;
using System.Security.Claims;
using Bit.Core.Utilities;
using Bit.Core.Models.Data;

namespace Bit.Core.Context
{
    public class CurrentContext : ICurrentContext
    {
        private bool _builtHttpContext;
        private bool _builtClaimsPrincipal;

        public virtual HttpContext HttpContext { get; set; }
        public virtual Guid? UserId { get; set; }
        public virtual User User { get; set; }
        public virtual string DeviceIdentifier { get; set; }
        public virtual DeviceType? DeviceType { get; set; }
        public virtual string IpAddress { get; set; }
        public virtual List<CurrentContentOrganization> Organizations { get; set; }
        public virtual Guid? InstallationId { get; set; }
        public virtual Guid? OrganizationId { get; set; }

        public async virtual Task BuildAsync(HttpContext httpContext, GlobalSettings globalSettings)
        {
            if (_builtHttpContext)
            {
                return;
            }

            _builtHttpContext = true;
            HttpContext = httpContext;
            await BuildAsync(httpContext.User, globalSettings);

            if (DeviceIdentifier == null && httpContext.Request.Headers.ContainsKey("Device-Identifier"))
            {
                DeviceIdentifier = httpContext.Request.Headers["Device-Identifier"];
            }

            if (httpContext.Request.Headers.ContainsKey("Device-Type") &&
                Enum.TryParse(httpContext.Request.Headers["Device-Type"].ToString(), out DeviceType dType))
            {
                DeviceType = dType;
            }
        }

        public async virtual Task BuildAsync(ClaimsPrincipal user, GlobalSettings globalSettings)
        {
            if (_builtClaimsPrincipal)
            {
                return;
            }

            _builtClaimsPrincipal = true;
            IpAddress = HttpContext.GetIpAddress(globalSettings);
            await SetContextAsync(user);
        }

        public virtual Task SetContextAsync(ClaimsPrincipal user)
        {
            if (user == null || !user.Claims.Any())
            {
                return Task.FromResult(0);
            }

            var claimsDict = user.Claims.GroupBy(c => c.Type).ToDictionary(c => c.Key, c => c.Select(v => v));

            var subject = GetClaimValue(claimsDict, "sub");
            if (Guid.TryParse(subject, out var subIdGuid))
            {
                UserId = subIdGuid;
            }

            var clientId = GetClaimValue(claimsDict, "client_id");
            var clientSubject = GetClaimValue(claimsDict, "client_sub");
            var orgApi = false;
            if (clientSubject != null)
            {
                if (clientId?.StartsWith("installation.") ?? false)
                {
                    if (Guid.TryParse(clientSubject, out var idGuid))
                    {
                        InstallationId = idGuid;
                    }
                }
                else if (clientId?.StartsWith("organization.") ?? false)
                {
                    if (Guid.TryParse(clientSubject, out var idGuid))
                    {
                        OrganizationId = idGuid;
                        orgApi = true;
                    }
                }
            }

            DeviceIdentifier = GetClaimValue(claimsDict, "device");

            Organizations = new List<CurrentContentOrganization>();
            if (claimsDict.ContainsKey("orgowner"))
            {
                Organizations.AddRange(claimsDict["orgowner"].Select(c =>
                    new CurrentContentOrganization
                    {
                        Id = new Guid(c.Value),
                        Type = OrganizationUserType.Owner
                    }));
            }
            else if (orgApi && OrganizationId.HasValue)
            {
                Organizations.Add(new CurrentContentOrganization
                {
                    Id = OrganizationId.Value,
                    Type = OrganizationUserType.Owner
                });
            }

            if (claimsDict.ContainsKey("orgadmin"))
            {
                Organizations.AddRange(claimsDict["orgadmin"].Select(c =>
                    new CurrentContentOrganization
                    {
                        Id = new Guid(c.Value),
                        Type = OrganizationUserType.Admin
                    }));
            }

            if (claimsDict.ContainsKey("orguser"))
            {
                Organizations.AddRange(claimsDict["orguser"].Select(c =>
                    new CurrentContentOrganization
                    {
                        Id = new Guid(c.Value),
                        Type = OrganizationUserType.User
                    }));
            }

            if (claimsDict.ContainsKey("orgmanager"))
            {
                Organizations.AddRange(claimsDict["orgmanager"].Select(c =>
                    new CurrentContentOrganization
                    {
                        Id = new Guid(c.Value),
                        Type = OrganizationUserType.Manager
                    }));
            }

            if (claimsDict.ContainsKey("orgcustom"))
            {
                Organizations.AddRange(claimsDict["orgcustom"].Select(c =>
                    new CurrentContentOrganization
                    {
                        Id = new Guid(c.Value),
                        Type = OrganizationUserType.Custom,
                        Permissions = SetOrganizationPermissionsFromClaims(c.Value, claimsDict)
                    }));
            }

            return Task.FromResult(0);
        }

        public bool OrganizationUser(Guid orgId)
        {
            return Organizations?.Any(o => o.Id == orgId) ?? false;
        }

        public bool OrganizationManager(Guid orgId)
        {
            return Organizations?.Any(o => o.Id == orgId &&
                (o.Type == OrganizationUserType.Owner || o.Type == OrganizationUserType.Admin ||
                    o.Type == OrganizationUserType.Manager)) ?? false;
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

        public bool OrganizationCustom(Guid orgId)
        {
            return Organizations?.Any(o => o.Id == orgId && o.Type == OrganizationUserType.Custom) ?? false;
        }
        
        public bool AccessBusinessPortal(Guid orgId)
        {
            return OrganizationAdmin(orgId) || (Organizations?.Any(o => o.Id == orgId 
                        && (o.Permissions?.AccessBusinessPortal ?? false)) ?? false);
        }

        public bool AccessEventLogs(Guid orgId)
        {
            return OrganizationAdmin(orgId) || (Organizations?.Any(o => o.Id == orgId 
                        && (o.Permissions?.AccessEventLogs ?? false)) ?? false);
        }

        public bool AccessImportExport(Guid orgId)
        {
            return OrganizationAdmin(orgId) || (Organizations?.Any(o => o.Id == orgId 
                        && (o.Permissions?.AccessImportExport ?? false)) ?? false);
        }

        public bool AccessReports(Guid orgId)
        {
            return OrganizationAdmin(orgId) || (Organizations?.Any(o => o.Id == orgId 
                        && (o.Permissions?.AccessReports ?? false)) ?? false);
        }

        public bool ManageAllCollections(Guid orgId)
        {
            return OrganizationAdmin(orgId) || (Organizations?.Any(o => o.Id == orgId 
                        && (o.Permissions?.ManageAllCollections ?? false)) ?? false);
        }

        public bool ManageAssignedCollections(Guid orgId)
        {
            return OrganizationManager(orgId) || (Organizations?.Any(o => o.Id == orgId 
                        && (o.Permissions?.ManageAssignedCollections ?? false)) ?? false);
        }

        public bool ManageGroups(Guid orgId)
        {
            return OrganizationAdmin(orgId) || (Organizations?.Any(o => o.Id == orgId 
                        && (o.Permissions?.ManageGroups ?? false)) ?? false);
        }

        public bool ManagePolicies(Guid orgId)
        {
            return OrganizationAdmin(orgId) || (Organizations?.Any(o => o.Id == orgId 
                        && (o.Permissions?.ManagePolicies ?? false)) ?? false);
        }

        public bool ManageSso(Guid orgId)
        {
            return OrganizationAdmin(orgId) || (Organizations?.Any(o => o.Id == orgId 
                        && (o.Permissions?.ManageSso ?? false)) ?? false);
        }

        public bool ManageUsers(Guid orgId)
        {
            return OrganizationAdmin(orgId) || (Organizations?.Any(o => o.Id == orgId 
                        && (o.Permissions?.ManageUsers ?? false)) ?? false);
        }

        public async Task<ICollection<CurrentContentOrganization>> OrganizationMembershipAsync(
            IOrganizationUserRepository organizationUserRepository, Guid userId)
        {
            if (Organizations == null)
            {
                var userOrgs = await organizationUserRepository.GetManyByUserAsync(userId);
                Organizations = userOrgs.Where(ou => ou.Status == OrganizationUserStatusType.Confirmed)
                    .Select(ou => new CurrentContentOrganization(ou)).ToList();
            }
            return Organizations;
        }

        private string GetClaimValue(Dictionary<string, IEnumerable<Claim>> claims, string type)
        {
            if (!claims.ContainsKey(type))
            {
                return null;
            }

            return claims[type].FirstOrDefault()?.Value;
        }

        private Permissions SetOrganizationPermissionsFromClaims(string organizationId, Dictionary<string, IEnumerable<Claim>> claimsDict)
        {
            bool hasClaim(string claimKey) 
            {
                return claimsDict.ContainsKey(claimKey) ? 
                    claimsDict[claimKey].Any(x => x.Value == organizationId) : false;
            }

            return new Permissions
            {
                AccessBusinessPortal = hasClaim("accessbusinessportal"),
                AccessEventLogs = hasClaim("accesseventlogs"),
                AccessImportExport = hasClaim("accessimportexport"),
                AccessReports = hasClaim("accessreports"),
                ManageAllCollections = hasClaim("manageallcollections"),
                ManageAssignedCollections = hasClaim("manageassignedcollections"),
                ManageGroups = hasClaim("managegroups"),
                ManagePolicies = hasClaim("managepolicies"),
                ManageSso = hasClaim("managesso"),
                ManageUsers = hasClaim("manageusers")
            };
        }
    }
}
