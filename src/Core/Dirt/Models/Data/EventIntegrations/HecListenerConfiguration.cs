using Bit.Core.Dirt.Enums;
using Bit.Core.Settings;

namespace Bit.Core.Dirt.Models.Data.EventIntegrations;

public class HecListenerConfiguration(GlobalSettings globalSettings)
    : ListenerConfiguration(globalSettings), IIntegrationListenerConfiguration
{
    public IntegrationType IntegrationType
    {
        get => IntegrationType.Hec;
    }

    public string EventQueueName
    {
        get => _globalSettings.EventLogging.RabbitMq.HecEventsQueueName;
    }

    public string IntegrationQueueName
    {
        get => _globalSettings.EventLogging.RabbitMq.HecIntegrationQueueName;
    }

    public string IntegrationRetryQueueName
    {
        get => _globalSettings.EventLogging.RabbitMq.HecIntegrationRetryQueueName;
    }

    public string EventSubscriptionName
    {
        get => _globalSettings.EventLogging.AzureServiceBus.HecEventSubscriptionName;
    }

    public string IntegrationSubscriptionName
    {
        get => _globalSettings.EventLogging.AzureServiceBus.HecIntegrationSubscriptionName;
    }
}
