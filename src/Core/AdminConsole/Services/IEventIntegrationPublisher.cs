using Bit.Core.AdminConsole.Models.Data.Integrations;

namespace Bit.Core.Services;

public interface IEventIntegrationPublisher : IAsyncDisposable
{
    Task PublishAsync(IIntegrationMessage message);
    Task PublishEventAsync(string body);
}
