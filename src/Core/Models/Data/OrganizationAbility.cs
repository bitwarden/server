using System;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Data
{
    public class OrganizationAbility
    {
        public OrganizationAbility() { }

        public OrganizationAbility(Organization organization)
        {
            Id = organization.Id;
            UseEvents = organization.UseEvents;
            Use2fa = organization.Use2fa;
            Enabled = organization.Enabled;
        }

        public Guid Id { get; set; }
        public bool UseEvents { get; set; }
        public bool Use2fa { get; set; }
        public bool Enabled { get; set; }
    }
}
