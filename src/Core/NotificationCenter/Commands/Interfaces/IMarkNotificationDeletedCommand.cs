#nullable enable
namespace Bit.Core.NotificationCenter.Commands.Interfaces;

public interface IMarkNotificationDeletedCommand
{
    Task MarkDeletedAsync(Guid notificationId);
}
