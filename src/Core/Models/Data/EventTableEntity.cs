using System;
using System.Collections.Generic;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Bit.Core.Models.Data
{
    public class EventTableEntity : TableEntity, IEvent
    {
        public EventTableEntity() { }

        private EventTableEntity(IEvent e)
        {
            Date = e.Date;
            Type = e.Type;
            UserId = e.UserId;
            OrganizationId = e.OrganizationId;
            CipherId = e.CipherId;
            CollectionId = e.CollectionId;
            GroupId = e.GroupId;
            OrganizationUserId = e.OrganizationUserId;
            ActingUserId = e.ActingUserId;
        }

        public DateTime Date { get; set; }
        public EventType Type { get; set; }
        public Guid? UserId { get; set; }
        public Guid? OrganizationId { get; set; }
        public Guid? CipherId { get; set; }
        public Guid? CollectionId { get; set; }
        public Guid? GroupId { get; set; }
        public Guid? OrganizationUserId { get; set; }
        public Guid? ActingUserId { get; set; }

        public override IDictionary<string, EntityProperty> WriteEntity(OperationContext operationContext)
        {
            var result = base.WriteEntity(operationContext);
            if(result.ContainsKey(nameof(Type)))
            {
                result[nameof(Type)] = new EntityProperty((int)Type);
            }
            else
            {
                result.Add(nameof(Type), new EntityProperty((int)Type));
            }
            return result;
        }

        public override void ReadEntity(IDictionary<string, EntityProperty> properties, OperationContext operationContext)
        {
            base.ReadEntity(properties, operationContext);
            if(properties.ContainsKey(nameof(Type)) && properties[nameof(Type)].Int32Value.HasValue)
            {
                Type = (EventType)properties[nameof(Type)].Int32Value;
            }
        }

        public static List<EventTableEntity> IndexEvent(IEvent e)
        {
            if(e.OrganizationId.HasValue)
            {
                return IndexOrgEvent(e);
            }
            else
            {
                return new List<EventTableEntity> { IndexUserEvent(e) };
            }
        }

        private static List<EventTableEntity> IndexOrgEvent(IEvent e)
        {
            var uniquifier = Guid.NewGuid();
            var pKey = $"OrganizationId={e.OrganizationId}";
            var dateKey = CoreHelpers.DateTimeToTableStorageKey(e.Date);

            var entities = new List<EventTableEntity>
            {
                new EventTableEntity(e)
                {
                    PartitionKey = pKey,
                    RowKey = string.Format("Date={0}__Uniquifier={1}", dateKey, uniquifier)
                }
            };

            if(e.ActingUserId.HasValue)
            {
                entities.Add(new EventTableEntity(e)
                {
                    PartitionKey = pKey,
                    RowKey = string.Format("ActingUserId={0}__Date={1}__Uniquifier={2}", e.ActingUserId, dateKey, uniquifier)
                });
            }

            if(e.CipherId.HasValue)
            {
                entities.Add(new EventTableEntity(e)
                {
                    PartitionKey = pKey,
                    RowKey = string.Format("CipherId={0}__Date={1}__Uniquifier={2}", e.CipherId, dateKey, uniquifier)
                });
            }

            return entities;
        }

        private static EventTableEntity IndexUserEvent(IEvent e)
        {
            var uniquifier = Guid.NewGuid();
            return new EventTableEntity(e)
            {
                PartitionKey = $"UserId={e.UserId}",
                RowKey = string.Format("Date={0}__Uniquifier={1}", CoreHelpers.DateTimeToTableStorageKey(e.Date), uniquifier)
            };
        }
    }
}
