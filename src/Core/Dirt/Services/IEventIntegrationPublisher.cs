using Bit.Core.Dirt.Models.Data.EventIntegrations;

namespace Bit.Core.Dirt.Services;

public interface IEventIntegrationPublisher : IAsyncDisposable
{
    Task PublishAsync(IIntegrationMessage message);
    Task PublishEventAsync(string body, string? organizationId);
}
