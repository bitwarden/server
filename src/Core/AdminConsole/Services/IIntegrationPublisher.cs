using Bit.Core.AdminConsole.Models.Data.Integrations;

namespace Bit.Core.Services;

public interface IIntegrationPublisher
{
    Task PublishAsync(IIntegrationMessage message);
}
