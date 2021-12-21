using System;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Bit.Core.Models.Business;
using Bit.Core.Settings;
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

        public AzureQueueReferenceEventService(
            GlobalSettings globalSettings)
        {
            _queueClient = new QueueClient(globalSettings.Events.ConnectionString, _queueName);
            _globalSettings = globalSettings;
        }

        public async Task RaiseEventAsync(ReferenceEvent referenceEvent)
        {
            await SendMessageAsync(referenceEvent);
        }

        private async Task SendMessageAsync(ReferenceEvent referenceEvent)
        {
            if (_globalSettings.SelfHosted)
            {
                // Ignore for self-hosted
                return;
            }
            try
            {
                var message = JsonConvert.SerializeObject(referenceEvent, _jsonSerializerSettings);
                // Messages need to be base64 encoded
                var encodedMessage = Convert.ToBase64String(Encoding.UTF8.GetBytes(message));
                await _queueClient.SendMessageAsync(encodedMessage);
            }
            catch
            {
                // Ignore failure
            }
        }
    }
}
