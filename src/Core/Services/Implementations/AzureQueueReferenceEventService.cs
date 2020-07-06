using System.Threading.Tasks;
using Azure.Storage.Queues;
using Bit.Core.Models;
using Newtonsoft.Json;

namespace Bit.Core.Services
{
    public class AzureQueueReferenceEventService : IReferenceEventService
    {
        private const string QueueName = "reference-events";

        private readonly QueueClient _queueClient;
        private readonly GlobalSettings _globalSettings;

        public AzureQueueReferenceEventService (
            GlobalSettings globalSettings)
        {
            _queueClient = new QueueClient(globalSettings.Storage.ConnectionString, QueueName);
            _globalSettings = globalSettings;
        }

        public async Task RaiseEventAsync(IReferenceable reference, string eventType,
            object additionalInfo = null)
        {
            await SendMessageAsync(reference, eventType, additionalInfo);
        }

        private async Task SendMessageAsync(IReferenceable reference, string eventType,
            object additionalInfo)
        {
            if (_globalSettings.SelfHosted)
            {
                return;
            }
            try
            {
                var message = JsonConvert.SerializeObject(new
                {
                    id = reference.Id,
                    type = reference.IsUser() ? "user" : "organization",
                    eventType,
                    referenceId = reference?.ReferenceId,
                    info = additionalInfo,
                });
                await _queueClient.SendMessageAsync(message);
            }
            catch
            {
                // Ignore failure
            }
        }
    }
}
