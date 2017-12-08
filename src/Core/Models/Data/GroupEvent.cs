using System;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Data
{
    public class GroupEvent : EventTableEntity
    {
        public GroupEvent(Group group, Guid actingUserId, EventType type)
        {
            OrganizationId = group.OrganizationId;
            GroupId = group.Id;
            Type = (int)type;
            ActingUserId = actingUserId;
            Date = DateTime.UtcNow;

            PartitionKey = $"OrganizationId={OrganizationId}";
            RowKey = string.Format("Date={0}__ActingUserId={1}__Type={2}",
                CoreHelpers.DateTimeToTableStorageKey(Date), ActingUserId, Type);
        }
    }
}
