using System.Text;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Integrations;
using Bit.Core.Settings;
using RabbitMQ.Client;

namespace Bit.Core.Services;

public class RabbitMqIntegrationPublisher : IIntegrationPublisher, IAsyncDisposable
{
    private readonly ConnectionFactory _factory;
    private readonly Lazy<Task<IConnection>> _lazyConnection;
    private readonly string _exchangeName;

    public RabbitMqIntegrationPublisher(GlobalSettings globalSettings)
    {
        _factory = new ConnectionFactory
        {
            HostName = globalSettings.EventLogging.RabbitMq.HostName,
            UserName = globalSettings.EventLogging.RabbitMq.Username,
            Password = globalSettings.EventLogging.RabbitMq.Password
        };
        _exchangeName = globalSettings.EventLogging.RabbitMq.IntegrationExchangeName;

        _lazyConnection = new Lazy<Task<IConnection>>(CreateConnectionAsync);
    }

    public async Task PublishAsync(IIntegrationMessage message)
    {
        var routingKey = message.IntegrationType.ToRoutingKey();
        var connection = await _lazyConnection.Value;
        await using var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(exchange: _exchangeName, type: ExchangeType.Direct, durable: true);

        var body = Encoding.UTF8.GetBytes(message.ToJson());

        await channel.BasicPublishAsync(exchange: _exchangeName, routingKey: routingKey, body: body);
    }

    public async ValueTask DisposeAsync()
    {
        if (_lazyConnection.IsValueCreated)
        {
            var connection = await _lazyConnection.Value;
            await connection.DisposeAsync();
        }
    }

    private async Task<IConnection> CreateConnectionAsync()
    {
        return await _factory.CreateConnectionAsync();
    }
}
