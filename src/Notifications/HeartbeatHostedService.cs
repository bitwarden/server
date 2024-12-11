using Bit.Core.Settings;
using Microsoft.AspNetCore.SignalR;

namespace Bit.Notifications;

public class HeartbeatHostedService : IHostedService, IDisposable
{
    private readonly ILogger _logger;
    private readonly IHubContext<NotificationsHub> _hubContext;
    private readonly GlobalSettings _globalSettings;

    private Task _executingTask;
    private CancellationTokenSource _cts;

    public HeartbeatHostedService(
        ILogger<HeartbeatHostedService> logger,
        IHubContext<NotificationsHub> hubContext,
        GlobalSettings globalSettings
    )
    {
        _logger = logger;
        _hubContext = hubContext;
        _globalSettings = globalSettings;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _executingTask = ExecuteAsync(_cts.Token);
        return _executingTask.IsCompleted ? _executingTask : Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_executingTask == null)
        {
            return;
        }
        _logger.LogWarning("Stopping service.");
        _cts.Cancel();
        await Task.WhenAny(_executingTask, Task.Delay(-1, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
    }

    public void Dispose() { }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await _hubContext.Clients.All.SendAsync("Heartbeat");
            await Task.Delay(120000, cancellationToken);
        }
        _logger.LogWarning("Done with heartbeat.");
    }
}
