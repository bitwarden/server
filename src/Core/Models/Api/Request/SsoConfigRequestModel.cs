using System;

namespace Bit.Core.Models.Api
{
    public class SsoConfigRequestModel
    {
        public long? Id { get; set; }
        public bool Enabled { get; set; }
        public Guid OrganizationId { get; set; }
        public string Data { get; set; }
    }
}
