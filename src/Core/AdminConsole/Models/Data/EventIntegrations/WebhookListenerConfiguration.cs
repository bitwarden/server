using Bit.Core.Enums;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

public class WebhookListenerConfiguration(GlobalSettings globalSettings)
    : ListenerConfiguration(globalSettings), IntegrationListenerConfiguration
{
    public IntegrationType IntegrationType
    {
        get => IntegrationType.Webhook;
    }

    public string EventQueueName
    {
        get => _globalSettings.EventLogging.RabbitMq.WebhookEventsQueueName;
    }

    public string IntegrationQueueName
    {
        get => _globalSettings.EventLogging.RabbitMq.WebhookIntegrationQueueName;
    }

    public string IntegrationRetryQueueName
    {
        get => _globalSettings.EventLogging.RabbitMq.WebhookIntegrationRetryQueueName;
    }

    public string EventSubscriotionName
    {
        get => _globalSettings.EventLogging.AzureServiceBus.WebhookEventSubscriptionName;
    }

    public string IntegrationSubscriptionName
    {
        get => _globalSettings.EventLogging.AzureServiceBus.WebhookIntegrationSubscriptionName;
    }
}
