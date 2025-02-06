#nullable enable
namespace Bit.Core.NotificationCenter.Commands.Interfaces;

public interface IMarkNotificationReadCommand
{
    Task MarkReadAsync(Guid notificationId);
}
