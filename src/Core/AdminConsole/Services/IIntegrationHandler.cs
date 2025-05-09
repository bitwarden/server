using Bit.Core.Models.Data.Integrations;

namespace Bit.Core.Services;

public interface IIntegrationHandler
{
    Task<IntegrationHandlerResult> HandleAsync(string json);
}

public interface IIntegrationHandler<T> : IIntegrationHandler
{
    Task<IntegrationHandlerResult> HandleAsync(IntegrationMessage<T> message);
}

public abstract class IntegrationHandlerBase<T> : IIntegrationHandler<T>
{
    public async Task<IntegrationHandlerResult> HandleAsync(string json)
    {
        var message = IntegrationMessage<T>.FromJson(json);
        return await HandleAsync(message);
    }

    public abstract Task<IntegrationHandlerResult> HandleAsync(IntegrationMessage<T> message);
}
