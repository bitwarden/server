using System;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Data
{
    public class CollectionEvent : EventTableEntity
    {
        public CollectionEvent(Collection collection, Guid actingUserId, EventType type)
        {
            OrganizationId = collection.OrganizationId;
            CollectionId = collection.Id;
            Type = (int)type;
            ActingUserId = actingUserId;

            Timestamp = DateTime.UtcNow;
            PartitionKey = $"OrganizationId={OrganizationId}";
            RowKey = string.Format("Date={0}__ActingUserId={1}__Type={2}",
                CoreHelpers.DateTimeToTableStorageKey(Timestamp.DateTime), ActingUserId, Type);
        }
    }
}
