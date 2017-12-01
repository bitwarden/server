using System;
using System.Collections.Generic;
using Bit.Core.Enums;
using Microsoft.WindowsAzure.Storage.Table;

namespace Bit.Core.Models.Data
{
    public class EventTableEntity : TableEntity
    {
        public EventType Type { get; set; }
        public Guid? UserId { get; set; }
        public Guid? OrganizationId { get; set; }
        public Guid? CipherId { get; set; }
        public ICollection<Guid> CipherIds { get; set; }
    }
}
