using System;

namespace Bit.Core.Models.Data
{
    public class GroupUserUserDetails
    {
        public Guid OrganizationUserId { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid GroupId { get; set; }
        public bool AccessAll { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public Enums.OrganizationUserStatusType Status { get; set; }
        public Enums.OrganizationUserType Type { get; set; }
    }
}
