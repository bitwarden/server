using System;
using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage.Table;

namespace Bit.Core.Models.Data
{
    public class EventTableEntity : TableEntity
    {
        public int Type { get; set; }
        public Guid? UserId { get; set; }
        public Guid? OrganizationId { get; set; }
        public Guid? CipherId { get; set; }
        public ICollection<Guid> CipherIds { get; set; }
        public Guid? CollectionId { get; set; }
        public Guid? GroupId { get; set; }
        public Guid? OrganizationUserId { get; set; }
        public Guid? ActingUserId { get; set; }
    }
}
