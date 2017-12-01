using System;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Data
{
    public class CipherEvent : EventTableEntity
    {
        public CipherEvent(Cipher cipher, EventType type)
        {
            OrganizationId = cipher.OrganizationId;
            UserId = cipher.UserId;
            CipherId = cipher.Id;
            Type = (int)type;

            Timestamp = DateTime.UtcNow;
            if(OrganizationId.HasValue)
            {
                PartitionKey = $"OrganizationId={OrganizationId}";
            }
            else
            {
                PartitionKey = $"UserId={UserId}";
            }

            RowKey = string.Format("Date={0}__CipherId={1}__Type={2}",
                CoreHelpers.DateTimeToTableStorageKey(Timestamp.DateTime), CipherId, Type);
        }
    }
}
