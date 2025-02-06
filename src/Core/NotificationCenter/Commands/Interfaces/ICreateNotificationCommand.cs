#nullable enable
using Bit.Core.NotificationCenter.Entities;

namespace Bit.Core.NotificationCenter.Commands.Interfaces;

public interface ICreateNotificationCommand
{
    Task<Notification> CreateAsync(Notification notification);
}
