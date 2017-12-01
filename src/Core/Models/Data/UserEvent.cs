using System;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Data
{
    public class UserEvent : EventTableEntity
    {
        public UserEvent(Guid userId, EventType type)
        {
            PartitionKey = $"UserId={userId}";
            RowKey = string.Format("Date={0}__Type={1}",
                CoreHelpers.DateTimeToTableStorageKey(), type);

            UserId = userId;
            Type = type;
        }

        public UserEvent(Guid userId, Guid organizationId, EventType type)
        {
            PartitionKey = $"OrganizationId={organizationId}";
            RowKey = string.Format("Date={0}__UserId={1}__Type={2}",
                CoreHelpers.DateTimeToTableStorageKey(), userId, type);

            OrganizationId = organizationId;
            UserId = userId;
            Type = type;
        }
    }
}
