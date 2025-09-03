using Azure.Messaging.ServiceBus;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;

namespace Bit.Core.Services;

public interface IAzureServiceBusService : IEventIntegrationPublisher, IAsyncDisposable
{
    ServiceBusProcessor CreateProcessor(string topicName, string subscriptionName, ServiceBusProcessorOptions options);
    Task PublishToRetryAsync(IIntegrationMessage message);
}
