using System;
using Bit.Core.Utilities;

namespace Bit.Core.Domains
{
    public class Share : IDataObject<Guid>
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid CipherId { get; set; }
        public string Key { get; set; }
        public string Permissions { get; set; }
        public Enums.ShareStatusType Status { get; set; }
        public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
        public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

        public void SetNewId()
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
