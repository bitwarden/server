using Bit.Core.Enums;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

public class HecListenerConfiguration(GlobalSettings globalSettings)
    : ListenerConfiguration(globalSettings), IntegrationListenerConfiguration
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

    public string EventSubscriotionName
    {
        get => _globalSettings.EventLogging.AzureServiceBus.HecEventSubscriptionName;
    }

    public string IntegrationSubscriptionName
    {
        get => _globalSettings.EventLogging.AzureServiceBus.HecIntegrationSubscriptionName;
    }
}
