using System.Text;
using System.Text.Json;
using Bit.Core.Models.Data;
using Bit.Core.Settings;
using RabbitMQ.Client;

namespace Bit.Core.Services;
public class RabbitMqEventWriteService : IEventWriteService
{
    private readonly ConnectionFactory _factory;
    private readonly Lazy<Task<IConnection>> _lazyConnection;
    private readonly string _exchangeName;

    public RabbitMqEventWriteService(GlobalSettings globalSettings)
    {
        _factory = new ConnectionFactory
        {
            HostName = globalSettings.RabbitMq.HostName,
            UserName = globalSettings.RabbitMq.Username,
            Password = globalSettings.RabbitMq.Password
        };
        _exchangeName = globalSettings.RabbitMq.ExchangeName;

        _lazyConnection = new Lazy<Task<IConnection>>(CreateConnectionAsync);
    }

    public async Task CreateAsync(IEvent e)
    {
        var connection = await _lazyConnection.Value;
        using var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(exchange: _exchangeName, type: ExchangeType.Fanout);

        var message = JsonSerializer.Serialize(e);
        var body = Encoding.UTF8.GetBytes(message);

        await channel.BasicPublishAsync(exchange: _exchangeName, routingKey: string.Empty, body: body);
    }

    public async Task CreateManyAsync(IEnumerable<IEvent> events)
    {
        var connection = await _lazyConnection.Value;
        using var channel = await connection.CreateChannelAsync();
        await channel.ExchangeDeclareAsync(exchange: _exchangeName, type: ExchangeType.Fanout);

        foreach (var e in events)
        {
            var message = JsonSerializer.Serialize(e);
            var body = Encoding.UTF8.GetBytes(message);

            await channel.BasicPublishAsync(exchange: _exchangeName, routingKey: string.Empty, body: body);
        }
    }

    private async Task<IConnection> CreateConnectionAsync()
    {
        return await _factory.CreateConnectionAsync();
    }
}
