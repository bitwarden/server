namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

public interface EventListenerConfiguration
{
    public string EventQueueName { get; }
    public string EventSubscriotionName { get; }
    public string EventTopicName { get; }
}
