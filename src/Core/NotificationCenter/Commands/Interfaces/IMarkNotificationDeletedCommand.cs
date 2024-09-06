#nullable enable
using Bit.Core.NotificationCenter.Entities;

namespace Bit.Core.NotificationCenter.Commands.Interfaces;

public interface IMarkNotificationDeletedCommand
{
    Task MarkDeletedAsync(Notification notification);
}
