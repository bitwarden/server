using System;

namespace Bit.Core.Models.Table
{
    public class CollectionGroup
    {
        public Guid CollectionId { get; set; }
        public Guid GroupId { get; set; }
        public bool ReadOnly { get; set; }
    }
}
