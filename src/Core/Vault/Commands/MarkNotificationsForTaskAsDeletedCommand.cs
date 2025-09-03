using Bit.Core.NotificationCenter.Repositories;
using Bit.Core.Platform.Push;
using Bit.Core.Vault.Commands.Interfaces;

namespace Bit.Core.Vault.Commands;

public class MarkNotificationsForTaskAsDeletedCommand : IMarkNotificationsForTaskAsDeletedCommand
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IPushNotificationService _pushNotificationService;

    public MarkNotificationsForTaskAsDeletedCommand(
        INotificationRepository notificationRepository,
        IPushNotificationService pushNotificationService)
    {
        _notificationRepository = notificationRepository;
        _pushNotificationService = pushNotificationService;

    }

    public async Task MarkAsDeletedAsync(Guid taskId)
    {
        var userIds = await _notificationRepository.MarkNotificationsAsDeletedByTask(taskId);

        // For each user associated with the notifications, send a push notification so local tasks can be updated.
        var uniqueUserIds = userIds.Distinct();
        foreach (var id in uniqueUserIds)
        {
            await _pushNotificationService.PushRefreshSecurityTasksAsync(id);
        }
    }
}
