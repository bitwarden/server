using System;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Data
{
    public class OrganizationEvent : EventTableEntity
    {
        public OrganizationEvent(Guid organizationId, EventType type)
        {
            OrganizationId = organizationId;
            Type = (int)type;

            Timestamp = DateTime.UtcNow;
            PartitionKey = $"OrganizationId={OrganizationId}";
            RowKey = string.Format("Date={0}__Type={1}",
                CoreHelpers.DateTimeToTableStorageKey(Timestamp.DateTime), Type);
        }

        public OrganizationEvent(Guid organizationId, Guid userId, EventType type)
        {
            OrganizationId = organizationId;
            UserId = userId;
            Type = (int)type;

            Timestamp = DateTime.UtcNow;
            PartitionKey = $"OrganizationId={OrganizationId}";
            RowKey = string.Format("Date={0}__UserId={1}__Type={2}",
                CoreHelpers.DateTimeToTableStorageKey(Timestamp.DateTime), UserId, Type);
        }
    }
}
