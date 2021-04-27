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
    public class AzureQueueEventWriteService : AzureQueueService<IEvent>, IEventWriteService
    {
        protected override QueueClient QueueClient { get; }
        protected override JsonSerializerSettings JsonSettings { get; } = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        public AzureQueueEventWriteService(
            GlobalSettings globalSettings)
        {
            QueueClient = new QueueClient(globalSettings.Events.ConnectionString, "event");
        }
    }
}
