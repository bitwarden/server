using Bit.Core.Enums;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

public class TeamsListenerConfiguration(GlobalSettings globalSettings) :
    ListenerConfiguration(globalSettings), IIntegrationListenerConfiguration
{
    public IntegrationType IntegrationType
    {
        get => IntegrationType.Teams;
    }

    public string EventQueueName
    {
        get => _globalSettings.EventLogging.RabbitMq.TeamsEventsQueueName;
    }

    public string IntegrationQueueName
    {
        get => _globalSettings.EventLogging.RabbitMq.TeamsIntegrationQueueName;
    }

    public string IntegrationRetryQueueName
    {
        get => _globalSettings.EventLogging.RabbitMq.TeamsIntegrationRetryQueueName;
    }

    public string EventSubscriptionName
    {
        get => _globalSettings.EventLogging.AzureServiceBus.TeamsEventSubscriptionName;
    }

    public string IntegrationSubscriptionName
    {
        get => _globalSettings.EventLogging.AzureServiceBus.TeamsIntegrationSubscriptionName;
    }
}
