using System;

namespace Bit.Core.Models.Table
{
    public class SsoUser
    {
        public Guid UserId { get; set; }
        public Guid OrganizationId { get; set; }
        public string ExternalId { get; set; }
    }
}
