using System;

namespace Bit.Core.Models.Data
{
    public class OrganizationUserOrganizationDetails
    {
        public Guid OrganizationId { get; set; }
        public Guid? UserId { get; set; }
        public string Name { get; set; }
        public bool UseGroups { get; set; }
        public int Seats { get; set; }
        public int MaxCollections { get; set; }
        public string Key { get; set; }
        public Enums.OrganizationUserStatusType Status { get; set; }
        public Enums.OrganizationUserType Type { get; set; }
        public bool Enabled { get; set; }
    }
}
