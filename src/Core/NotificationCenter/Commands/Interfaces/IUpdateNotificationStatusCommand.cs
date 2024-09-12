#nullable enable
using Bit.Core.NotificationCenter.Entities;

namespace Bit.Core.NotificationCenter.Commands.Interfaces;

public interface IUpdateNotificationStatusCommand
{
    Task UpdateAsync(NotificationStatus notificationStatus);
}
