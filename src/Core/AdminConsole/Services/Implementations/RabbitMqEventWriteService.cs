using System.Text.Json;
using Bit.Core.Models.Data;
using Bit.Core.Settings;
using RabbitMQ.Client;

namespace Bit.Core.Services;
public class RabbitMqEventWriteService : IEventWriteService, IAsyncDisposable
{
    private readonly ConnectionFactory _factory;
    private readonly Lazy<Task<IConnection>> _lazyConnection;
    private readonly string _exchangeName;

    public RabbitMqEventWriteService(GlobalSettings globalSettings)
    {
        _factory = new ConnectionFactory
        {
            HostName = globalSettings.EventLogging.RabbitMq.HostName,
            UserName = globalSettings.EventLogging.RabbitMq.Username,
            Password = globalSettings.EventLogging.RabbitMq.Password
        };
        _exchangeName = globalSettings.EventLogging.RabbitMq.EventExchangeName;

        _lazyConnection = new Lazy<Task<IConnection>>(CreateConnectionAsync);
    }

    public async Task CreateAsync(IEvent e)
    {
        var connection = await _lazyConnection.Value;
        using var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(exchange: _exchangeName, type: ExchangeType.Fanout, durable: true);

        var body = JsonSerializer.SerializeToUtf8Bytes(e);

        await channel.BasicPublishAsync(exchange: _exchangeName, routingKey: string.Empty, body: body);
    }

    public async Task CreateManyAsync(IEnumerable<IEvent> events)
    {
        var connection = await _lazyConnection.Value;
        using var channel = await connection.CreateChannelAsync();
        await channel.ExchangeDeclareAsync(exchange: _exchangeName, type: ExchangeType.Fanout, durable: true);

        var body = JsonSerializer.SerializeToUtf8Bytes(events);

        await channel.BasicPublishAsync(exchange: _exchangeName, routingKey: string.Empty, body: body);
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
