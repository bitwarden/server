using System.Threading.Tasks;
using System.Collections.Generic;
using Azure.Storage.Queues;
using Newtonsoft.Json;
using Bit.Core.Models.Data;
using Bit.Core.Settings;
using System.Linq;
using System.Text;

namespace Bit.Core.Services
{
    public class AzureQueueEventWriteService : IEventWriteService
    {
        private const int _maxMessageBody = 64000; // 64 MB
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
            if (e?.Any() != true)
            {
                return;
            }

            if (e.Count == 1)
            {
                await CreateAsync(e.First());
                return;
            }

            foreach (var json in SerializeMany(e))
            {
                await _queueClient.SendMessageAsync(json);
            }
        }

        private IEnumerable<string> SerializeMany(IList<IEvent> events)
        {
            var eventsList = new List<List<IEvent>> { new List<IEvent>() };
            var strings = new List<string>();
            var messageLength = 2; // to account for json array brackets "[]"
            foreach (var (ev, jsonEvent) in events.Select(e => (e, JsonConvert.SerializeObject(e, _jsonSettings))))
            {

                var eventLength = jsonEvent.Length + 1; // To account for json array comma
                if (messageLength + eventLength > _maxMessageBody)
                {
                    eventsList.Add(new List<IEvent> { ev });
                    messageLength = 2 + eventLength;
                }
                else
                {
                    eventsList.Last().Add(ev);
                    messageLength += eventLength;
                }
            }
            return eventsList.Select(l => JsonConvert.SerializeObject(l, _jsonSettings));
        }
    }
}
