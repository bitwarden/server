﻿namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

public interface IEventListenerConfiguration
{
    public string EventQueueName { get; }
    public string EventSubscriptionName { get; }
    public string EventTopicName { get; }
}
