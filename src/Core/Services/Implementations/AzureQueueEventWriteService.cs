using System.Threading.Tasks;
using System.Collections.Generic;
using Azure.Storage.Queues;
using Newtonsoft.Json;
using Bit.Core.Models.Data;

namespace Bit.Core.Services
{
    public class AzureQueueEventWriteService : IEventWriteService
    {
        private readonly QueueClient _queueClient;

        private JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        public AzureQueueEventWriteService(
            GlobalSettings globalSettings)
        {
            _queueClient = new QueueClient(globalSettings.Events.ConnectionString, "event");
        }

        public async Task CreateAsync(IEvent e)
        {
            var json = JsonConvert.SerializeObject(e, _jsonSettings);
            await _queueClient.SendMessageAsync(json);
        }

        public async Task CreateManyAsync(IList<IEvent> e)
        {
            var json = JsonConvert.SerializeObject(e, _jsonSettings);
            await _queueClient.SendMessageAsync(json);
        }
    }
}
