using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Bit.Core.Models.Data;
using Bit.Core.Settings;
using Newtonsoft.Json;

namespace Bit.Core.Services
{
    public class AzureQueueEventWriteService : AzureQueueService<IEvent>, IEventWriteService
    {
        public AzureQueueEventWriteService(GlobalSettings globalSettings) : base(
            new QueueClient(globalSettings.Events.ConnectionString, "event"),
            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })
        { }
    }
}
