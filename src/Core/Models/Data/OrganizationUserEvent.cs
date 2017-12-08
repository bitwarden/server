using System;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Data
{
    public class OrganizationUserEvent : EventTableEntity
    {
        public OrganizationUserEvent(OrganizationUser organizationUser, Guid actingUserId, EventType type)
        {
            OrganizationId = organizationUser.OrganizationId;
            UserId = organizationUser.UserId;
            OrganizationUserId = organizationUser.Id;
            Type = (int)type;
            ActingUserId = actingUserId;
            Date = DateTime.UtcNow;

            PartitionKey = $"OrganizationId={OrganizationId}";
            RowKey = string.Format("Date={0}__ActingUserId={1}__Type={2}",
                CoreHelpers.DateTimeToTableStorageKey(Date), ActingUserId, Type);
        }
    }
}
