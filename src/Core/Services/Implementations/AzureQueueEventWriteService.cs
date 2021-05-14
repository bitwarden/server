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
        public AzureQueueEventWriteService(
            GlobalSettings globalSettings) : base(new QueueClient(globalSettings.Events.ConnectionString, "event"),
            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }) 
        { }
    }
}
