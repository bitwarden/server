using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;

namespace Bit.Core.Test.Dirt.Models.Data.EventIntegrations;

public class TestListenerConfiguration : IIntegrationListenerConfiguration
{
    public string EventQueueName => "event_queue";
    public string EventSubscriptionName => "event_subscription";
    public string EventTopicName => "event_topic";
    public IntegrationType IntegrationType => IntegrationType.Webhook;
    public string IntegrationQueueName => "integration_queue";
    public string IntegrationRetryQueueName => "integration_retry_queue";
    public string IntegrationSubscriptionName => "integration_subscription";
    public string IntegrationTopicName => "integration_topic";
    public int MaxRetries => 3;
    public int EventMaxConcurrentCalls => 1;
    public int EventPrefetchCount => 0;
    public int IntegrationMaxConcurrentCalls => 1;
    public int IntegrationPrefetchCount => 0;
    public string RoutingKey => IntegrationType.ToRoutingKey();
}
