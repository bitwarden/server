using System;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Table
{
    public class Group : IDataObject<Guid>
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public string Name { get; set; }
        public bool AccessAll { get; set; }
        public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
        public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

        public void SetNewId()
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
