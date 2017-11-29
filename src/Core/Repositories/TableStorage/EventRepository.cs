using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.Utilities;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Bit.Core.Repositories.TableStorage
{
    public class EventRepository
    {
        public EventRepository(GlobalSettings globalSettings)
        {
            var storageAccount = CloudStorageAccount.Parse(globalSettings.Storage.ConnectionString);
            var tableClient = storageAccount.CreateCloudTableClient();
            Table = tableClient.GetTableReference("event");
        }

        protected CloudTable Table { get; set; }

        public ICollection<EventTableEntiity> GetManyByUser(Guid userId, DateTime startDate, DateTime endDate)
        {
            var start = CoreHelpers.DateTimeToTableStorageKey(startDate);
            var end = CoreHelpers.DateTimeToTableStorageKey(endDate);

            return Table.CreateQuery<EventTableEntiity>().Where(e =>
                e.PartitionKey == $"UserId={userId}" &&
                e.RowKey.CompareTo($"{start}_") >= 0 &&
                e.RowKey.CompareTo($"{end}`") <= 0).ToList();
        }

        public ICollection<EventTableEntiity> GetManyByOrganization(Guid organizationId, DateTime startDate, DateTime endDate)
        {
            var start = CoreHelpers.DateTimeToTableStorageKey(startDate);
            var end = CoreHelpers.DateTimeToTableStorageKey(endDate);

            return Table.CreateQuery<EventTableEntiity>().Where(e =>
                e.PartitionKey == $"OrganizationId={organizationId}" &&
                e.RowKey.CompareTo($"{start}_") >= 0 &&
                e.RowKey.CompareTo($"{end}`") <= 0).ToList();
        }

        public async Task CreateAsync(ITableEntity entity)
        {
            await Table.ExecuteAsync(TableOperation.Insert(entity));
        }

        public void CreateManyAsync(IEnumerable<ITableEntity> entities)
        {
            if(!entities?.Any() ?? true)
            {
                return;
            }

            // A batch insert can only contain 100 entities at a time
            var iterations = entities.Count() / 100;
            for(var i = 0; i <= iterations; i++)
            {
                var batch = new TableBatchOperation();
                var batchEntities = entities.Skip(i * 100).Take(100);
                if(!batchEntities.Any())
                {
                    break;
                }

                foreach(var entity in batchEntities)
                {
                    batch.InsertOrReplace(entity);
                }

                Table.ExecuteBatch(batch);
            }
        }
    }

    public class UserEvent : EventTableEntiity
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

    public class CipherEvent : EventTableEntiity
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

    public class OrganizationEvent : EventTableEntiity
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

    public class EventTableEntiity : TableEntity
    {
        public EventType Type { get; set; }
        public Guid? UserId { get; set; }
        public Guid? OrganizationId { get; set; }
        public Guid? CipherId { get; set; }
        public ICollection<Guid> CipherIds { get; set; }
    }
}
