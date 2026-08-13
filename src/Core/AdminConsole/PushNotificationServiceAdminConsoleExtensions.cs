using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;
using Bit.Core.Models;

namespace Bit.Core.Platform.Push;

public static class PushNotificationServiceAdminConsoleExtensions
{
    public static Task PushSyncOrgKeysAsync(this IPushNotificationService service, Guid userId)
        => service.PushAsync(new PushNotification<UserPushNotification>
        {
            Type = PushType.SyncOrgKeys,
            Target = NotificationTarget.User,
            TargetId = userId,
            Payload = new UserPushNotification
            {
                UserId = userId,
                Date = DateTime.UtcNow,
            },
            ExcludeCurrentContext = false,
        });

    public static Task PushSyncOrganizationsAsync(this IPushNotificationService service, Guid userId)
        => service.PushAsync(new PushNotification<UserPushNotification>
        {
            Type = PushType.SyncOrganizations,
            Target = NotificationTarget.User,
            TargetId = userId,
            Payload = new UserPushNotification
            {
                UserId = userId,
                Date = DateTime.UtcNow,
            },
            ExcludeCurrentContext = false,
        });

    public static Task PushSyncOrganizationCollectionManagementSettingsAsync(
        this IPushNotificationService service, Organization organization)
        => service.PushAsync(new PushNotification<OrganizationCollectionManagementPushNotification>
        {
            Type = PushType.SyncOrganizationCollectionSettingChanged,
            Target = NotificationTarget.Organization,
            TargetId = organization.Id,
            Payload = new OrganizationCollectionManagementPushNotification
            {
                OrganizationId = organization.Id,
                LimitCollectionCreation = organization.LimitCollectionCreation,
                LimitCollectionDeletion = organization.LimitCollectionDeletion,
                LimitItemDeletion = organization.LimitItemDeletion,
            },
            ExcludeCurrentContext = false,
        });
}
