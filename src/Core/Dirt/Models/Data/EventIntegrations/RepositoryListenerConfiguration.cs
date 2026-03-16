using Bit.Core.Settings;

namespace Bit.Core.Dirt.Models.Data.EventIntegrations;

public class RepositoryListenerConfiguration(GlobalSettings globalSettings)
    : ListenerConfiguration(globalSettings), IEventListenerConfiguration
{
    public string EventQueueName
    {
        get => _globalSettings.EventLogging.RabbitMq.EventRepositoryQueueName;
    }

    public string EventSubscriptionName
    {
        get => _globalSettings.EventLogging.AzureServiceBus.EventRepositorySubscriptionName;
    }
}
