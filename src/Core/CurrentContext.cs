using System;
using System.Collections.Generic;
using System.Linq;
using Bit.Core.Models.Table;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Http;
using Bit.Core.Repositories;
using System.Threading.Tasks;

namespace Bit.Core
{
    public class CurrentContext
    {
        private string _ip;
        private Dictionary<Guid, ICollection<OrganizationUser>> _orgUsers =
            new Dictionary<Guid, ICollection<OrganizationUser>>();

        public virtual HttpContext HttpContext { get; set; }
        public virtual Guid? UserId { get; set; }
        public virtual User User { get; set; }
        public virtual string DeviceIdentifier { get; set; }
        public virtual DeviceType? DeviceType { get; set; }
        public virtual string IpAddress => GetRequestIp();
        public virtual List<CurrentContentOrganization> Organizations { get; set; } =
            new List<CurrentContentOrganization>();
        public virtual Guid? InstallationId { get; set; }

        public bool OrganizationUser(Guid orgId)
        {
            return Organizations.Any(o => o.Id == orgId);
        }
        public bool OrganizationAdmin(Guid orgId)
        {
            return Organizations.Any(o => o.Id == orgId &&
                (o.Type == OrganizationUserType.Owner || o.Type == OrganizationUserType.Admin));
        }
        public bool OrganizationOwner(Guid orgId)
        {
            return Organizations.Any(o => o.Id == orgId && o.Type == OrganizationUserType.Owner);
        }

        public async Task<ICollection<OrganizationUser>> OrganizationMembershipAsync(
            IOrganizationUserRepository organizationUserRepository, Guid userId)
        {
            if(!_orgUsers.ContainsKey(userId))
            {
                _orgUsers.Add(userId, await organizationUserRepository.GetManyByUserAsync(userId));
            }

            return _orgUsers[userId];
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

        public class CurrentContentOrganization
        {
            public Guid Id { get; set; }
            public OrganizationUserType Type { get; set; }
        }
    }
}
