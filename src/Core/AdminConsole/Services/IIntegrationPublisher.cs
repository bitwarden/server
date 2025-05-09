using Bit.Core.Models.Data.Integrations;

namespace Bit.Core.Services;

public interface IIntegrationPublisher
{
    Task PublishAsync(IIntegrationMessage message);
}
