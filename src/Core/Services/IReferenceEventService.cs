using System.Threading.Tasks;
using Bit.Core.Models;

namespace Bit.Core.Services
{
    public interface IReferenceEventService
    {
        Task RaiseEventAsync(IReferenceable reference, string eventType,
            object additionalInfo = null);
    }
}
