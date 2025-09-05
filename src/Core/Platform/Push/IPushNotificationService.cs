using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.Tools.Entities;
using Bit.Core.Vault.Entities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Platform.Push;

/// <summary>
/// Used to Push notifications to end-user devices.
/// </summary>
/// <remarks>
/// New notifications should not be wired up inside this service. You may either directly call the
/// <see cref="PushAsync"/> method in your service to send your notification or if you want your notification
/// sent by other teams you can make an extension method on this service with a well typed definition
/// of your notification. You may also make your own service that injects this and exposes methods for each of
/// your notifications.
/// </remarks>
public interface IPushNotificationService
{
    private const string ServiceDeprecation = "Do not use the services exposed here, instead use your own services injected in your service.";

    [Obsolete(ServiceDeprecation, DiagnosticId = "BWP0001")]
    Guid InstallationId { get; }

    [Obsolete(ServiceDeprecation, DiagnosticId = "BWP0001")]
    TimeProvider TimeProvider { get; }

    [Obsolete(ServiceDeprecation, DiagnosticId = "BWP0001")]
    ILogger Logger { get; }

    #region Legacy method, to be removed soon.
    Task PushSyncCipherCreateAsync(Cipher cipher, IEnumerable<Guid> collectionIds)
        => PushCipherAsync(cipher, PushType.SyncCipherCreate, collectionIds);

    Task PushSyncCipherUpdateAsync(Cipher cipher, IEnumerable<Guid> collectionIds)
        => PushCipherAsync(cipher, PushType.SyncCipherUpdate, collectionIds);

    Task PushSyncCipherDeleteAsync(Cipher cipher)
        => PushCipherAsync(cipher, PushType.SyncLoginDelete, null);

    Task PushSyncFolderCreateAsync(Folder folder)
        => PushAsync(new PushNotification<SyncFolderPushNotification>
        {
            Type = PushType.SyncFolderCreate,
            Target = NotificationTarget.User,
            TargetId = folder.UserId,
            Payload = new SyncFolderPushNotification
            {
                Id = folder.Id,
                UserId = folder.UserId,
                RevisionDate = folder.RevisionDate,
            },
            ExcludeCurrentContext = true,
        });

    Task PushSyncFolderUpdateAsync(Folder folder)
        => PushAsync(new PushNotification<SyncFolderPushNotification>
        {
            Type = PushType.SyncFolderUpdate,
            Target = NotificationTarget.User,
            TargetId = folder.UserId,
            Payload = new SyncFolderPushNotification
            {
                Id = folder.Id,
                UserId = folder.UserId,
                RevisionDate = folder.RevisionDate,
            },
            ExcludeCurrentContext = true,
        });

    Task PushSyncFolderDeleteAsync(Folder folder)
        => PushAsync(new PushNotification<SyncFolderPushNotification>
        {
            Type = PushType.SyncFolderDelete,
            Target = NotificationTarget.User,
            TargetId = folder.UserId,
            Payload = new SyncFolderPushNotification
            {
                Id = folder.Id,
                UserId = folder.UserId,
                RevisionDate = folder.RevisionDate,
            },
            ExcludeCurrentContext = true,
        });

    Task PushSyncCiphersAsync(Guid userId)
        => PushAsync(new PushNotification<UserPushNotification>
        {
            Type = PushType.SyncCiphers,
            Target = NotificationTarget.User,
            TargetId = userId,
            Payload = new UserPushNotification
            {
                UserId = userId,
#pragma warning disable BWP0001 // Type or member is obsolete
                Date = TimeProvider.GetUtcNow().UtcDateTime,
#pragma warning restore BWP0001 // Type or member is obsolete
            },
            ExcludeCurrentContext = false,
        });

    Task PushSyncVaultAsync(Guid userId)
        => PushAsync(new PushNotification<UserPushNotification>
        {
            Type = PushType.SyncVault,
            Target = NotificationTarget.User,
            TargetId = userId,
            Payload = new UserPushNotification
            {
                UserId = userId,
#pragma warning disable BWP0001 // Type or member is obsolete
                Date = TimeProvider.GetUtcNow().UtcDateTime,
#pragma warning restore BWP0001 // Type or member is obsolete
            },
            ExcludeCurrentContext = false,
        });

    Task PushSyncOrganizationsAsync(Guid userId)
        => PushAsync(new PushNotification<UserPushNotification>
        {
            Type = PushType.SyncOrganizations,
            Target = NotificationTarget.User,
            TargetId = userId,
            Payload = new UserPushNotification
            {
                UserId = userId,
#pragma warning disable BWP0001 // Type or member is obsolete
                Date = TimeProvider.GetUtcNow().UtcDateTime,
#pragma warning restore BWP0001 // Type or member is obsolete
            },
            ExcludeCurrentContext = false,
        });

    Task PushSyncOrgKeysAsync(Guid userId)
        => PushAsync(new PushNotification<UserPushNotification>
        {
            Type = PushType.SyncOrgKeys,
            Target = NotificationTarget.User,
            TargetId = userId,
            Payload = new UserPushNotification
            {
                UserId = userId,
#pragma warning disable BWP0001 // Type or member is obsolete
                Date = TimeProvider.GetUtcNow().UtcDateTime,
#pragma warning restore BWP0001 // Type or member is obsolete
            },
            ExcludeCurrentContext = false,
        });

    Task PushSyncSettingsAsync(Guid userId)
        => PushAsync(new PushNotification<UserPushNotification>
        {
            Type = PushType.SyncSettings,
            Target = NotificationTarget.User,
            TargetId = userId,
            Payload = new UserPushNotification
            {
                UserId = userId,
#pragma warning disable BWP0001 // Type or member is obsolete
                Date = TimeProvider.GetUtcNow().UtcDateTime,
#pragma warning restore BWP0001 // Type or member is obsolete
            },
            ExcludeCurrentContext = false,
        });

    Task PushLogOutAsync(Guid userId, bool excludeCurrentContextFromPush = false)
        => PushAsync(new PushNotification<UserPushNotification>
        {
            Type = PushType.LogOut,
            Target = NotificationTarget.User,
            TargetId = userId,
            Payload = new UserPushNotification
            {
                UserId = userId,
#pragma warning disable BWP0001 // Type or member is obsolete
                Date = TimeProvider.GetUtcNow().UtcDateTime,
#pragma warning restore BWP0001 // Type or member is obsolete
            },
            ExcludeCurrentContext = excludeCurrentContextFromPush,
        });

    Task PushSyncSendCreateAsync(Send send)
    {
        if (send.UserId.HasValue)
        {
            return PushAsync(new PushNotification<SyncSendPushNotification>
            {
                Type = PushType.SyncSendCreate,
                Target = NotificationTarget.User,
                TargetId = send.UserId.Value,
                Payload = new SyncSendPushNotification
                {
                    Id = send.Id,
                    UserId = send.UserId.Value,
                    RevisionDate = send.RevisionDate,
                },
                ExcludeCurrentContext = true,
            });
        }

        return Task.CompletedTask;
    }

    Task PushSyncSendUpdateAsync(Send send)
    {
        if (send.UserId.HasValue)
        {
            return PushAsync(new PushNotification<SyncSendPushNotification>
            {
                Type = PushType.SyncSendUpdate,
                Target = NotificationTarget.User,
                TargetId = send.UserId.Value,
                Payload = new SyncSendPushNotification
                {
                    Id = send.Id,
                    UserId = send.UserId.Value,
                    RevisionDate = send.RevisionDate,
                },
                ExcludeCurrentContext = true,
            });
        }

        return Task.CompletedTask;
    }

    Task PushSyncSendDeleteAsync(Send send)
    {
        if (send.UserId.HasValue)
        {
            return PushAsync(new PushNotification<SyncSendPushNotification>
            {
                Type = PushType.SyncSendDelete,
                Target = NotificationTarget.User,
                TargetId = send.UserId.Value,
                Payload = new SyncSendPushNotification
                {
                    Id = send.Id,
                    UserId = send.UserId.Value,
                    RevisionDate = send.RevisionDate,
                },
                ExcludeCurrentContext = true,
            });
        }

        return Task.CompletedTask;
    }

    Task PushNotificationAsync(Notification notification)
    {
        var message = new NotificationPushNotification
        {
            Id = notification.Id,
            Priority = notification.Priority,
            Global = notification.Global,
            ClientType = notification.ClientType,
            UserId = notification.UserId,
            OrganizationId = notification.OrganizationId,
#pragma warning disable BWP0001 // Type or member is obsolete
            InstallationId = notification.Global ? InstallationId : null,
#pragma warning restore BWP0001 // Type or member is obsolete
            TaskId = notification.TaskId,
            Title = notification.Title,
            Body = notification.Body,
            CreationDate = notification.CreationDate,
            RevisionDate = notification.RevisionDate,
        };

        NotificationTarget target;
        Guid targetId;

        if (notification.Global)
        {
            // TODO: Think about this a bit more
            target = NotificationTarget.Installation;
#pragma warning disable BWP0001 // Type or member is obsolete
            targetId = InstallationId;
#pragma warning restore BWP0001 // Type or member is obsolete
        }
        else if (notification.UserId.HasValue)
        {
            target = NotificationTarget.User;
            targetId = notification.UserId.Value;
        }
        else if (notification.OrganizationId.HasValue)
        {
            target = NotificationTarget.Organization;
            targetId = notification.OrganizationId.Value;
        }
        else
        {
#pragma warning disable BWP0001 // Type or member is obsolete
            Logger.LogWarning("Invalid notification id {NotificationId} push notification", notification.Id);
#pragma warning restore BWP0001 // Type or member is obsolete
            return Task.CompletedTask;
        }

        return PushAsync(new PushNotification<NotificationPushNotification>
        {
            Type = PushType.Notification,
            Target = target,
            TargetId = targetId,
            Payload = message,
            ExcludeCurrentContext = true,
            ClientType = notification.ClientType,
        });
    }

    Task PushNotificationStatusAsync(Notification notification, NotificationStatus notificationStatus)
    {
        var message = new NotificationPushNotification
        {
            Id = notification.Id,
            Priority = notification.Priority,
            Global = notification.Global,
            ClientType = notification.ClientType,
            UserId = notification.UserId,
            OrganizationId = notification.OrganizationId,
#pragma warning disable BWP0001 // Type or member is obsolete
            InstallationId = notification.Global ? InstallationId : null,
#pragma warning restore BWP0001 // Type or member is obsolete
            TaskId = notification.TaskId,
            Title = notification.Title,
            Body = notification.Body,
            CreationDate = notification.CreationDate,
            RevisionDate = notification.RevisionDate,
            ReadDate = notificationStatus.ReadDate,
            DeletedDate = notificationStatus.DeletedDate,
        };

        NotificationTarget target;
        Guid targetId;

        if (notification.Global)
        {
            // TODO: Think about this a bit more
            target = NotificationTarget.Installation;
#pragma warning disable BWP0001 // Type or member is obsolete
            targetId = InstallationId;
#pragma warning restore BWP0001 // Type or member is obsolete
        }
        else if (notification.UserId.HasValue)
        {
            target = NotificationTarget.User;
            targetId = notification.UserId.Value;
        }
        else if (notification.OrganizationId.HasValue)
        {
            target = NotificationTarget.Organization;
            targetId = notification.OrganizationId.Value;
        }
        else
        {
#pragma warning disable BWP0001 // Type or member is obsolete
            Logger.LogWarning("Invalid notification status id {NotificationId} push notification", notification.Id);
#pragma warning restore BWP0001 // Type or member is obsolete
            return Task.CompletedTask;
        }

        return PushAsync(new PushNotification<NotificationPushNotification>
        {
            Type = PushType.NotificationStatus,
            Target = target,
            TargetId = targetId,
            Payload = message,
            ExcludeCurrentContext = true,
            ClientType = notification.ClientType,
        });
    }

    Task PushAuthRequestAsync(AuthRequest authRequest)
        => PushAsync(new PushNotification<AuthRequestPushNotification>
        {
            Type = PushType.AuthRequest,
            Target = NotificationTarget.User,
            TargetId = authRequest.UserId,
            Payload = new AuthRequestPushNotification
            {
                Id = authRequest.Id,
                UserId = authRequest.UserId,
            },
            ExcludeCurrentContext = true,
        });

    Task PushAuthRequestResponseAsync(AuthRequest authRequest)
        => PushAsync(new PushNotification<AuthRequestPushNotification>
        {
            Type = PushType.AuthRequestResponse,
            Target = NotificationTarget.User,
            TargetId = authRequest.UserId,
            Payload = new AuthRequestPushNotification
            {
                Id = authRequest.Id,
                UserId = authRequest.UserId,
            },
            ExcludeCurrentContext = true,
        });

    Task PushSyncOrganizationCollectionManagementSettingsAsync(Organization organization)
        => PushAsync(new PushNotification<OrganizationCollectionManagementPushNotification>
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

    Task PushRefreshSecurityTasksAsync(Guid userId)
        => PushAsync(new PushNotification<UserPushNotification>
        {
            Type = PushType.RefreshSecurityTasks,
            Target = NotificationTarget.User,
            TargetId = userId,
            Payload = new UserPushNotification
            {
                UserId = userId,
#pragma warning disable BWP0001 // Type or member is obsolete
                Date = TimeProvider.GetUtcNow().UtcDateTime,
#pragma warning restore BWP0001 // Type or member is obsolete
            },
            ExcludeCurrentContext = false,
        });
    #endregion

    Task PushCipherAsync(Cipher cipher, PushType pushType, IEnumerable<Guid>? collectionIds);

    /// <summary>
    /// Pushes a notification to devices based on the settings given to us in <see cref="PushNotification{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the payload to be sent along with the notification.</typeparam>
    /// <param name="pushNotification"></param>
    /// <returns>A task that is NOT guarunteed to have sent the notification by the time the task resolves.</returns>
    Task PushAsync<T>(PushNotification<T> pushNotification)
        where T : class;
}
