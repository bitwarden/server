using System;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Data
{
    public class OrganizationEvent : EventTableEntity
    {
        public OrganizationEvent(Guid organizationId, EventType type)
        {
            PartitionKey = $"OrganizationId={organizationId}";
            RowKey = string.Format("Date={0}__Type={1}",
                CoreHelpers.DateTimeToTableStorageKey(), type);

            OrganizationId = organizationId;
            Type = type;
        }

        public OrganizationEvent(Guid organizationId, Guid userId, EventType type)
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
