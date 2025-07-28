using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

public class RepositoryListenerConfiguration(GlobalSettings globalSettings)
    : ListenerConfiguration(globalSettings), EventListenerConfiguration
{
    public string EventQueueName
    {
        get => _globalSettings.EventLogging.RabbitMq.EventRepositoryQueueName;
    }

    public string EventSubscriotionName
    {
        get => _globalSettings.EventLogging.AzureServiceBus.EventRepositorySubscriptionName;
    }
}
