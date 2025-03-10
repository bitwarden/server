#nullable enable
using Bit.Core.NotificationCenter.Entities;

namespace Bit.Core.NotificationCenter.Commands.Interfaces;

public interface ICreateNotificationStatusCommand
{
    Task<NotificationStatus> CreateAsync(NotificationStatus notificationStatus);
}
