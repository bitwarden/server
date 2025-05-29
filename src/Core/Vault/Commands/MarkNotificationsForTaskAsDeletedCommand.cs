using Bit.Core.Context;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Repositories;
using Bit.Core.Platform.Push;
using Bit.Core.Vault.Commands.Interfaces;

namespace Bit.Core.Vault.Commands;

public class MarkNotificationsForTaskAsDeletedCommand : IMarkNotificationsForTaskAsDeletedCommand
{
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationStatusRepository _notificationStatusRepository;
    private readonly ICurrentContext _currentContext;
    private readonly IPushNotificationService _pushNotificationService;

    public MarkNotificationsForTaskAsDeletedCommand(
        INotificationRepository notificationRepository,
        INotificationStatusRepository notificationStatusRepository,
        ICurrentContext currentContext,
        IPushNotificationService pushNotificationService)
    {
        _notificationRepository = notificationRepository;
        _notificationStatusRepository = notificationStatusRepository;
        _currentContext = currentContext;
        _pushNotificationService = pushNotificationService;

    }

    public async Task MarkAsDeletedAsync(Guid taskId)
    {
        var notifications = await _notificationRepository.GetNonDeletedByTaskIdAsync(taskId);

        foreach (var notification in notifications)
        {
            var notificationStatus = await _notificationStatusRepository.GetByNotificationIdAndUserIdAsync(notification.Id, _currentContext.UserId.Value);

            if (notificationStatus == null)
            {
                notificationStatus = new NotificationStatus
                {
                    NotificationId = notification.Id,
                    UserId = _currentContext.UserId.Value,
                    DeletedDate = DateTime.UtcNow
                };

                var newNotificationStatus = await _notificationStatusRepository.CreateAsync(notificationStatus);

                await _pushNotificationService.PushNotificationStatusAsync(notification, newNotificationStatus);
            }
            else
            {
                notificationStatus.DeletedDate = DateTime.UtcNow;

                await _notificationStatusRepository.UpdateAsync(notificationStatus);

                await _pushNotificationService.PushNotificationStatusAsync(notification, notificationStatus);
            }

        }
    }
}
