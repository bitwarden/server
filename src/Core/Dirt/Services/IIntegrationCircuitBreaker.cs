using Bit.Core.Dirt.Models.Data.EventIntegrations;

namespace Bit.Core.Dirt.Services;

public interface IIntegrationCircuitBreaker
{
    Task RecordResultAsync(IntegrationHandlerResult result);
}
