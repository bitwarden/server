using System.Threading.Tasks;
using Bit.Core.Models;

namespace Bit.Core.Services
{
    public class NoopReferenceEventService : IReferenceEventService
    {
        public Task RaiseEventAsync(IReferenceable reference, string eventType,
            object additionalInfo = null)
        {
            return Task.CompletedTask;
        }
    }
}
