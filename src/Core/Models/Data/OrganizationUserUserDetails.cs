using System;

namespace Bit.Core.Models.Data
{
    public class OrganizationUserUserDetails
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid? UserId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public Enums.OrganizationUserStatusType Status { get; set; }
        public Enums.OrganizationUserType Type { get; set; }
        public bool AccessAll { get; set; }
    }
}
