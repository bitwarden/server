using Bit.Core.Enums;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

public class SlackListenerConfiguration(GlobalSettings globalSettings) :
    ListenerConfiguration(globalSettings), IntegrationListenerConfiguration
{
    public IntegrationType IntegrationType
    {
        get => IntegrationType.Slack;
    }

    public string EventQueueName
    {
        get => _globalSettings.EventLogging.RabbitMq.SlackEventsQueueName;
    }

    public string IntegrationQueueName
    {
        get => _globalSettings.EventLogging.RabbitMq.SlackIntegrationQueueName;
    }

    public string IntegrationRetryQueueName
    {
        get => _globalSettings.EventLogging.RabbitMq.SlackIntegrationRetryQueueName;
    }

    public string EventSubscriotionName
    {
        get => _globalSettings.EventLogging.AzureServiceBus.SlackEventSubscriptionName;
    }

    public string IntegrationSubscriptionName
    {
        get => _globalSettings.EventLogging.AzureServiceBus.SlackIntegrationSubscriptionName;
    }
}
