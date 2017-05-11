using System;

namespace Bit.Core.Models.Data
{
    public class CollectionUserDetails
    {
        public Guid OrganizationUserId { get; set; }
        public bool AccessAll { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public Enums.OrganizationUserStatusType Status { get; set; }
        public Enums.OrganizationUserType Type { get; set; }
        public bool ReadOnly { get; set; }
    }
}
