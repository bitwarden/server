using System;
using System.Collections.Generic;
using System.Linq;
using Bit.Core.Models.Table;
using Bit.Core.Enums;

namespace Bit.Core
{
    public class CurrentContext
    {
        public virtual User User { get; set; }
        public virtual string DeviceIdentifier { get; set; }
        public virtual List<CurrentContentOrganization> Organizations { get; set; } = new List<CurrentContentOrganization>();
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

        public class CurrentContentOrganization
        {
            public Guid Id { get; set; }
            public OrganizationUserType Type { get; set; }
        }
    }
}
