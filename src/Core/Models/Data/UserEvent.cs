using System;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Data
{
    public class UserEvent : EventTableEntity
    {
        public UserEvent(Guid userId, EventType type)
        {
            UserId = userId;
            Type = (int)type;
            Date = DateTime.UtcNow;

            PartitionKey = $"UserId={UserId}";
            RowKey = string.Format("Date={0}__Type={1}",
                CoreHelpers.DateTimeToTableStorageKey(Date), Type);
        }

        public UserEvent(Guid userId, Guid organizationId, EventType type)
        {
            OrganizationId = organizationId;
            UserId = userId;
            Type = (int)type;
            Date = DateTime.UtcNow;
            
            PartitionKey = $"OrganizationId={OrganizationId}";
            RowKey = string.Format("Date={0}__UserId={1}__Type={2}",
                CoreHelpers.DateTimeToTableStorageKey(Date), UserId, Type);
        }
    }
}
