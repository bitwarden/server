using Bit.Core.Enums;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

public class DatadogListenerConfiguration(GlobalSettings globalSettings)
    : ListenerConfiguration(globalSettings), IIntegrationListenerConfiguration
{
    public IntegrationType IntegrationType
    {
        get => IntegrationType.Datadog;
    }

    public string EventQueueName
    {
        get => _globalSettings.EventLogging.RabbitMq.DatadogEventsQueueName;
    }

    public string IntegrationQueueName
    {
        get => _globalSettings.EventLogging.RabbitMq.DatadogIntegrationQueueName;
    }

    public string IntegrationRetryQueueName
    {
        get => _globalSettings.EventLogging.RabbitMq.DatadogIntegrationRetryQueueName;
    }

    public string EventSubscriptionName
    {
        get => _globalSettings.EventLogging.AzureServiceBus.DatadogEventSubscriptionName;
    }

    public string IntegrationSubscriptionName
    {
        get => _globalSettings.EventLogging.AzureServiceBus.DatadogIntegrationSubscriptionName;
    }
}
