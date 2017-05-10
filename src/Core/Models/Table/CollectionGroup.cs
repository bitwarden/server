using System;

namespace Bit.Core.Models.Table
{
    public class CollectionGroup
    {
        public Guid CollectionId { get; set; }
        public Guid OrganizationUserId { get; set; }
        public bool ReadOnly { get; set; }
    }
}
