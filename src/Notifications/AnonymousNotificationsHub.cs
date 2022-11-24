using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Bit.Notifications;

[AllowAnonymous]
public class AnonymousNotificationsHub : Microsoft.AspNetCore.SignalR.Hub, INotificationHub
{
    private readonly ILogger<AzureQueueHostedService> _logger;

    public AnonymousNotificationsHub(ILogger<AzureQueueHostedService> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var token = httpContext.Request.Query["Token"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(token))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, token);
        }

        _logger.LogInformation("AnonymousNotificationsHub connection {id} registered token {token}", Context.ConnectionId, token);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception ex)
    {
        var httpContext = Context.GetHttpContext();
        var token = httpContext.Request.Query["Token"].FirstOrDefault();
        if (ex == null)
        {
            _logger.LogInformation("AnonymousNotificationHub connection {id} disconnected for token {token}", Context.ConnectionId, token);
        }
        else
        {
            _logger.LogError(ex, "Disconnected for token {token} with exception", token);
        }
    }
}
