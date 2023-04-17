using Bit.Core.Tools.Models.Business;

namespace Bit.Core.Tools.Services;

public interface IReferenceEventService
{
    Task RaiseEventAsync(ReferenceEvent referenceEvent);
}
