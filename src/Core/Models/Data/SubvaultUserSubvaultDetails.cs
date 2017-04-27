using System;

namespace Bit.Core.Models.Data
{
    public class CollectionUserCollectionDetails
    {
        public Guid Id { get; set; }
        public Guid OrganizationUserId { get; set; }
        public string Name { get; set; }
        public Guid CollectionId { get; set; }
        public bool ReadOnly { get; set; }
    }
}
