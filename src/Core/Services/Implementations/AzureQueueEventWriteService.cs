using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Bit.Core.Models.Data;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.Core.Services
{
    public class AzureQueueEventWriteService : AzureQueueService<IEvent>, IEventWriteService
    {
        public AzureQueueEventWriteService(GlobalSettings globalSettings) : base(
            new QueueClient(globalSettings.Events.ConnectionString, "event"),
            JsonHelpers.IgnoreWritingNull)
        { }

        public Task CreateAsync(IEvent e) => CreateManyAsync(new[] { e });
    }
}
