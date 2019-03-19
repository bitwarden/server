using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Models.Data;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Bit.Core.Repositories.TableStorage
{
    public class InstallationDeviceRepository : IInstallationDeviceRepository
    {
        private readonly CloudTable _table;

        public InstallationDeviceRepository(GlobalSettings globalSettings)
            : this(globalSettings.Events.ConnectionString)
        { }

        public InstallationDeviceRepository(string storageConnectionString)
        {
            var storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            var tableClient = storageAccount.CreateCloudTableClient();
            _table = tableClient.GetTableReference("installationdevice");
        }

        public async Task UpsertAsync(InstallationDeviceEntity entity)
        {
            await _table.ExecuteAsync(TableOperation.InsertOrReplace(entity));
        }

        public async Task UpsertManyAsync(IList<InstallationDeviceEntity> entities)
        {
            if(!entities?.Any() ?? true)
            {
                return;
            }

            if(entities.Count == 1)
            {
                await UpsertAsync(entities.First());
                return;
            }

            var entityGroups = entities.GroupBy(ent => ent.PartitionKey);
            foreach(var group in entityGroups)
            {
                var groupEntities = group.ToList();
                if(groupEntities.Count == 1)
                {
                    await UpsertAsync(groupEntities.First());
                    continue;
                }

                // A batch insert can only contain 100 entities at a time
                var iterations = groupEntities.Count / 100;
                for(var i = 0; i <= iterations; i++)
                {
                    var batch = new TableBatchOperation();
                    var batchEntities = groupEntities.Skip(i * 100).Take(100);
                    if(!batchEntities.Any())
                    {
                        break;
                    }

                    foreach(var entity in batchEntities)
                    {
                        batch.InsertOrReplace(entity);
                    }

                    await _table.ExecuteBatchAsync(batch);
                }
            }
        }

        public async Task DeleteAsync(InstallationDeviceEntity entity)
        {
            await _table.ExecuteAsync(TableOperation.Delete(entity));
        }
    }
}
