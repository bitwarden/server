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

        public EventTableEntity(IEvent e)
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

            switch(e.Type)
            {
                case EventType.User_LoggedIn:
                case EventType.User_ChangedPassword:
                case EventType.User_Enabled2fa:
                case EventType.User_Disabled2fa:
                case EventType.User_Recovered2fa:
                case EventType.User_FailedLogIn:
                case EventType.User_FailedLogIn2fa:
                    if(e.OrganizationId.HasValue)
                    {
                        PartitionKey = $"OrganizationId={OrganizationId}";
                        RowKey = string.Format("Date={0}__UserId={1}__Type={2}",
                            CoreHelpers.DateTimeToTableStorageKey(Date), UserId, Type);
                    }
                    else
                    {
                        PartitionKey = $"UserId={UserId}";
                        RowKey = string.Format("Date={0}__Type={1}",
                            CoreHelpers.DateTimeToTableStorageKey(Date), Type);
                    }
                    break;
                case EventType.Cipher_Created:
                case EventType.Cipher_Updated:
                case EventType.Cipher_Deleted:
                case EventType.Cipher_AttachmentCreated:
                case EventType.Cipher_AttachmentDeleted:
                case EventType.Cipher_Shared:
                case EventType.Cipher_UpdatedCollections:
                    if(OrganizationId.HasValue)
                    {
                        PartitionKey = $"OrganizationId={OrganizationId}";
                        RowKey = string.Format("Date={0}__CipherId={1}__ActingUserId={2}__Type={3}",
                            CoreHelpers.DateTimeToTableStorageKey(Date), CipherId, ActingUserId, Type);
                    }
                    else
                    {
                        PartitionKey = $"UserId={UserId}";
                        RowKey = string.Format("Date={0}__CipherId={1}__Type={2}",
                            CoreHelpers.DateTimeToTableStorageKey(Date), CipherId, Type);
                    }
                    break;
                case EventType.Collection_Created:
                case EventType.Collection_Updated:
                case EventType.Collection_Deleted:
                    PartitionKey = $"OrganizationId={OrganizationId}";
                    RowKey = string.Format("Date={0}__ActingUserId={1}__Type={2}",
                        CoreHelpers.DateTimeToTableStorageKey(Date), ActingUserId, Type);
                    break;
                case EventType.Group_Created:
                case EventType.Group_Updated:
                case EventType.Group_Deleted:
                    PartitionKey = $"OrganizationId={OrganizationId}";
                    RowKey = string.Format("Date={0}__ActingUserId={1}__Type={2}",
                        CoreHelpers.DateTimeToTableStorageKey(Date), ActingUserId, Type);
                    break;
                case EventType.OrganizationUser_Invited:
                case EventType.OrganizationUser_Confirmed:
                case EventType.OrganizationUser_Updated:
                case EventType.OrganizationUser_Removed:
                case EventType.OrganizationUser_UpdatedGroups:
                    PartitionKey = $"OrganizationId={OrganizationId}";
                    RowKey = string.Format("Date={0}__ActingUserId={1}__Type={2}",
                        CoreHelpers.DateTimeToTableStorageKey(Date), ActingUserId, Type);
                    break;
                case EventType.Organization_Updated:
                    PartitionKey = $"OrganizationId={OrganizationId}";
                    RowKey = string.Format("Date={0}__ActingUserId={1}__Type={2}",
                        CoreHelpers.DateTimeToTableStorageKey(Date), ActingUserId, Type);
                    break;
                default:
                    break;
            }
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
    }
}
