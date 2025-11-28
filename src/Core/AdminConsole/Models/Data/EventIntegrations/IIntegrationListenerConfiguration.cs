using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

public interface IIntegrationListenerConfiguration : IEventListenerConfiguration
{
    public IntegrationType IntegrationType { get; }
    public string IntegrationQueueName { get; }
    public string IntegrationRetryQueueName { get; }
    public string IntegrationSubscriptionName { get; }
    public string IntegrationTopicName { get; }
    public int MaxRetries { get; }
    public int IntegrationPrefetchCount { get; }
    public int IntegrationMaxConcurrentCalls { get; }

    public string RoutingKey
    {
        get => IntegrationType.ToRoutingKey();
    }
}
