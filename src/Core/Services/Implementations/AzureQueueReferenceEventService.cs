using System.Threading.Tasks;
using Azure.Storage.Queues;
using Bit.Core.Models.Business;
using Newtonsoft.Json;

namespace Bit.Core.Services
{
    public class AzureQueueReferenceEventService : IReferenceEventService
    {
        private const string _queueName = "reference-events";

        private readonly QueueClient _queueClient;
        private readonly GlobalSettings _globalSettings;
        private readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
        };

        public AzureQueueReferenceEventService (
            GlobalSettings globalSettings)
        {
            _queueClient = new QueueClient(globalSettings.Storage.ConnectionString, _queueName);
            _globalSettings = globalSettings;
        }

        public async Task RaiseEventAsync(ReferenceEvent referenceEvent)
        {
            await SendMessageAsync(referenceEvent);
        }

        private async Task SendMessageAsync(ReferenceEvent referenceEvent)
        {
            if (_globalSettings.SelfHosted || string.IsNullOrWhiteSpace(referenceEvent.ReferenceId))
            {
                // Ignore for self-hosted, OR, where there is no ReferenceId
                return;
            }
            try
            {
                var message = JsonConvert.SerializeObject(referenceEvent, _jsonSerializerSettings);
                await _queueClient.SendMessageAsync(message);
            }
            catch
            {
                // Ignore failure
            }
        }
    }
}
