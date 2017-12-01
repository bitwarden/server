using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Data
{
    public class CipherEvent : EventTableEntity
    {
        public CipherEvent(Cipher cipher, EventType type)
        {
            if(cipher.OrganizationId.HasValue)
            {
                PartitionKey = $"OrganizationId={cipher.OrganizationId.Value}";
            }
            else
            {
                PartitionKey = $"UserId={cipher.UserId.Value}";
            }

            RowKey = string.Format("Date={0}__CipherId={1}__Type={2}",
                CoreHelpers.DateTimeToTableStorageKey(), cipher.Id, type);

            OrganizationId = cipher.OrganizationId;
            UserId = cipher.UserId;
            CipherId = cipher.Id;
            Type = type;
        }
    }
}
