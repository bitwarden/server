using System;
using System.Collections.Generic;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Microsoft.Azure.Cosmos.Table;

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
            PolicyId = e.PolicyId;
            GroupId = e.GroupId;
            OrganizationUserId = e.OrganizationUserId;
            DeviceType = e.DeviceType;
            IpAddress = e.IpAddress;
            ActingUserId = e.ActingUserId;
        }

        public DateTime Date { get; set; }
        public EventType Type { get; set; }
        public Guid? UserId { get; set; }
        public Guid? OrganizationId { get; set; }
        public Guid? CipherId { get; set; }
        public Guid? CollectionId { get; set; }
        public Guid? PolicyId { get; set; }
        public Guid? GroupId { get; set; }
        public Guid? OrganizationUserId { get; set; }
        public DeviceType? DeviceType { get; set; }
        public string IpAddress { get; set; }
        public Guid? ActingUserId { get; set; }

        public override IDictionary<string, EntityProperty> WriteEntity(OperationContext operationContext)
        {
            var result = base.WriteEntity(operationContext);

            var typeName = nameof(Type);
            if (result.ContainsKey(typeName))
            {
                result[typeName] = new EntityProperty((int)Type);
            }
            else
            {
                result.Add(typeName, new EntityProperty((int)Type));
            }

            var deviceTypeName = nameof(DeviceType);
            if (result.ContainsKey(deviceTypeName))
            {
                result[deviceTypeName] = new EntityProperty((int?)DeviceType);
            }
            else
            {
                result.Add(deviceTypeName, new EntityProperty((int?)DeviceType));
            }

            return result;
        }

        public override void ReadEntity(IDictionary<string, EntityProperty> properties,
            OperationContext operationContext)
        {
            base.ReadEntity(properties, operationContext);

            var typeName = nameof(Type);
            if (properties.ContainsKey(typeName) && properties[typeName].Int32Value.HasValue)
            {
                Type = (EventType)properties[typeName].Int32Value.Value;
            }

            var deviceTypeName = nameof(DeviceType);
            if (properties.ContainsKey(deviceTypeName) && properties[deviceTypeName].Int32Value.HasValue)
            {
                DeviceType = (DeviceType)properties[deviceTypeName].Int32Value.Value;
            }
        }

        public static List<EventTableEntity> IndexEvent(EventMessage e)
        {
            var uniquifier = e.IdempotencyId.GetValueOrDefault(Guid.NewGuid());
            var pKey = e.OrganizationId.HasValue ? $"OrganizationId={e.OrganizationId}" : $"UserId={e.UserId}";
            var dateKey = CoreHelpers.DateTimeToTableStorageKey(e.Date);

            var entities = new List<EventTableEntity>
            {
                new EventTableEntity(e)
                {
                    PartitionKey = pKey,
                    RowKey = string.Format("Date={0}__Uniquifier={1}", dateKey, uniquifier)
                }
            };

            if (e.OrganizationId.HasValue && e.ActingUserId.HasValue)
            {
                entities.Add(new EventTableEntity(e)
                {
                    PartitionKey = pKey,
                    RowKey = string.Format("ActingUserId={0}__Date={1}__Uniquifier={2}",
                        e.ActingUserId, dateKey, uniquifier)
                });
            }

            if (e.CipherId.HasValue)
            {
                entities.Add(new EventTableEntity(e)
                {
                    PartitionKey = pKey,
                    RowKey = string.Format("CipherId={0}__Date={1}__Uniquifier={2}",
                        e.CipherId, dateKey, uniquifier)
                });
            }

            return entities;
        }
    }
}
