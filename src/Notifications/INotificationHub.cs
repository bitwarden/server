namespace Bit.Notifications;

public interface INotificationHub
{
    Task OnConnectedAsync();
    Task OnDisconnectedAsync(Exception exception);
}
