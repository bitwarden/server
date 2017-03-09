using System;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Table
{
    public class SubvaultUser : IDataObject<Guid>
    {
        public Guid Id { get; set; }
        public Guid SubvaultId { get; set; }
        public Guid UserId { get; set; }
        public bool Admin { get; set; }
        public bool ReadOnly { get; set; }
        public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
        public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

        public void SetNewId()
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
