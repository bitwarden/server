using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;

namespace Bit.Services.Pam.Services;

public class ApproverInboxNotifier : IApproverInboxNotifier
{
    private readonly ICollectionRepository _collectionRepository;
    private readonly IPushNotificationService _pushNotificationService;

    public ApproverInboxNotifier(
        ICollectionRepository collectionRepository,
        IPushNotificationService pushNotificationService)
    {
        _collectionRepository = collectionRepository;
        _pushNotificationService = pushNotificationService;
    }

    public async Task NotifyCollectionApproversAsync(Guid collectionId)
    {
        var userIds = await _collectionRepository.GetManagingUserIdsAsync(collectionId);
        foreach (var userId in userIds)
        {
            await _pushNotificationService.PushAsync(new PushNotification<UserPushNotification>
            {
                Type = PushType.RefreshApproverInbox,
                Target = NotificationTarget.User,
                TargetId = userId,
                Payload = new UserPushNotification
                {
                    UserId = userId,
                    Date = DateTime.UtcNow,
                },
                ExcludeCurrentContext = false,
            });
        }
    }
}
