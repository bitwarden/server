﻿using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

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
}
