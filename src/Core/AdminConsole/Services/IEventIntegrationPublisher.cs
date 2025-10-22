using Bit.Core.AdminConsole.Models.Data.EventIntegrations;

namespace Bit.Core.Services;

public interface IEventIntegrationPublisher : IAsyncDisposable
{
    Task PublishAsync(IIntegrationMessage message);
    Task PublishEventAsync(string body, string organizationId);
}
