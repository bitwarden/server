using Bit.Core.Models.Business;

namespace Bit.Core.Services;

public class NoopReferenceEventService : IReferenceEventService
{
    public Task RaiseEventAsync(ReferenceEvent referenceEvent)
    {
        return Task.CompletedTask;
    }
}
