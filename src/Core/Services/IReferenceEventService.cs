using Bit.Core.Models.Business;

namespace Bit.Core.Services;

public interface IReferenceEventService
{
    Task RaiseEventAsync(ReferenceEvent referenceEvent);
}
