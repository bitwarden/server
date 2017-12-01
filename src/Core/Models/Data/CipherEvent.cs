using System;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Data
{
    public class CipherEvent : EventTableEntity
    {
        public CipherEvent(Cipher cipher, EventType type, Guid? actingUserId = null)
        {
            OrganizationId = cipher.OrganizationId;
            UserId = cipher.UserId;
            CipherId = cipher.Id;
            Type = (int)type;
            ActingUserId = actingUserId;

            Timestamp = DateTime.UtcNow;
            if(OrganizationId.HasValue)
            {
                UserId = null;
                PartitionKey = $"OrganizationId={OrganizationId}";
                RowKey = string.Format("Date={0}__CipherId={1}__ActingUserId={2}__Type={3}",
                    CoreHelpers.DateTimeToTableStorageKey(Timestamp.DateTime), CipherId, ActingUserId, Type);
            }
            else
            {
                PartitionKey = $"UserId={UserId}";
                RowKey = string.Format("Date={0}__CipherId={1}__Type={2}",
                    CoreHelpers.DateTimeToTableStorageKey(Timestamp.DateTime), CipherId, Type);
            }
        }
    }
}
