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
        private readonly QueueClient _queueClient;
        private const int MAX_MESSAGE_BODY = 128000;

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
            if (!e?.Any() ?? true)
            {
                return;
            }

            if (e.Count == 1)
            {
                await CreateAsync(e.First());
                return;
            }

            foreach(var json in SerializeMany(e))
            {
                await _queueClient.SendMessageAsync(json);
            }
        }

        private IEnumerable<string> SerializeMany(IList<IEvent> events)
        {
            var strings = new List<string>();
            var jsonEvents = events.Select(e => JsonConvert.SerializeObject(e, _jsonSettings));
            var stringBuilder = new StringBuilder("[");
            foreach(var jsonEvent in jsonEvents)
            {
                if (stringBuilder.Length + jsonEvent.Length + 2 < MAX_MESSAGE_BODY)
                {
                    stringBuilder.Append($",{jsonEvent}");
                }
                else
                {
                    stringBuilder.Append("]");
                    strings.Append(stringBuilder.ToString());
                    stringBuilder = new StringBuilder("[");
                }
            }
            stringBuilder.Append("]");
            strings.Append(stringBuilder.ToString());
            return strings;
        }
    }
}
