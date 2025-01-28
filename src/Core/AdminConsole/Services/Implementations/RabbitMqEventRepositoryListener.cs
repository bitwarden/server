using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class RabbitMqEventRepositoryListener : RabbitMqEventListenerBase
{
    private readonly IEventWriteService _eventWriteService;
    private readonly string _queueName;

    protected override string QueueName => _queueName;

    public RabbitMqEventRepositoryListener(
        IEventRepository eventRepository,
        ILogger<RabbitMqEventListenerBase> logger,
        GlobalSettings globalSettings)
        : base(logger, globalSettings)
    {
        _eventWriteService = new RepositoryEventWriteService(eventRepository);
        _queueName = globalSettings.EventLogging.RabbitMq.EventRepositoryQueueName;
    }

    protected override Task HandleMessageAsync(EventMessage eventMessage)
    {
        return _eventWriteService.CreateAsync(eventMessage);
    }
}
