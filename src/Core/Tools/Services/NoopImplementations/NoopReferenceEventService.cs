using Bit.Core.Tools.Models.Business;

namespace Bit.Core.Tools.Services;

public class NoopReferenceEventService : IReferenceEventService
{
    public Task RaiseEventAsync(ReferenceEvent referenceEvent)
    {
        return Task.CompletedTask;
    }
}
