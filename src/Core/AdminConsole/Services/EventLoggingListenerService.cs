using Microsoft.Extensions.Hosting;

namespace Bit.Core.Services;

public abstract class EventLoggingListenerService : BackgroundService
{
    protected readonly IEventMessageHandler _handler;

    protected EventLoggingListenerService(IEventMessageHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }
}
