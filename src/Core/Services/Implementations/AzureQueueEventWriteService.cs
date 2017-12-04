using System.Threading.Tasks;
using Bit.Core.Repositories;
using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace Bit.Core.Services
{
    public class AzureQueueEventWriteService : IEventWriteService
    {
        private readonly CloudQueue _queue;
        private readonly GlobalSettings _globalSettings;

        private JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        public AzureQueueEventWriteService(
            IEventRepository eventRepository,
            GlobalSettings globalSettings)
        {
            var storageAccount = CloudStorageAccount.Parse(globalSettings.Storage.ConnectionString);
            var queueClient = storageAccount.CreateCloudQueueClient();

            _queue = queueClient.GetQueueReference("event");
            _globalSettings = globalSettings;
        }

        public async Task CreateAsync(ITableEntity entity)
        {
            var json = JsonConvert.SerializeObject(entity, _jsonSettings);
            var message = new CloudQueueMessage(json);
            await _queue.AddMessageAsync(message);
        }

        public async Task CreateManyAsync(IList<ITableEntity> entities)
        {
            var json = JsonConvert.SerializeObject(entities, _jsonSettings);
            var message = new CloudQueueMessage(json);
            await _queue.AddMessageAsync(message);
        }
    }
}
