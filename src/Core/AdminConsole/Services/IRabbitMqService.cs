using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Bit.Core.Services;

public interface IRabbitMqService : IEventIntegrationPublisher
{
    Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default);
    Task CreateEventQueueAsync(string queueName, CancellationToken cancellationToken = default);
    Task CreateIntegrationQueuesAsync(
        string queueName,
        string retryQueueName,
        string routingKey,
        CancellationToken cancellationToken = default);
    Task PublishToRetryAsync(IChannel channel, IIntegrationMessage message, CancellationToken cancellationToken);
    Task PublishToDeadLetterAsync(IChannel channel, IIntegrationMessage message, CancellationToken cancellationToken);
    Task RepublishToRetryQueueAsync(IChannel channel, BasicDeliverEventArgs eventArgs);
}
