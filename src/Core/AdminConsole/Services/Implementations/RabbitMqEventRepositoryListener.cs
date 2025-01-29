using Bit.Core.Models.Data;
using Bit.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class RabbitMqEventRepositoryListener : RabbitMqEventListenerBase
{
    private readonly IEventWriteService _eventWriteService;
    private readonly string _queueName;

    protected override string QueueName => _queueName;

    public RabbitMqEventRepositoryListener(
        [FromKeyedServices("persistent")] IEventWriteService eventWriteService,
        ILogger<RabbitMqEventListenerBase> logger,
        GlobalSettings globalSettings)
        : base(logger, globalSettings)
    {
        _eventWriteService = eventWriteService;
        _queueName = globalSettings.EventLogging.RabbitMq.EventRepositoryQueueName;
    }

    protected override Task HandleMessageAsync(EventMessage eventMessage)
    {
        return _eventWriteService.CreateAsync(eventMessage);
    }
}
