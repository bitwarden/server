#nullable enable
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.Tools.Entities;
using Bit.Core.Vault.Entities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Platform.Push;

public interface IPushNotificationService
{
    Guid InstallationId { get; }
    TimeProvider TimeProvider { get; }
    ILogger Logger { get; }

#region Legacy method, to be removed soon.
    Task PushSyncCipherCreateAsync(Cipher cipher, IEnumerable<Guid> collectionIds)
        => PushCipherAsync(cipher, PushType.SyncCipherCreate, collectionIds);

    Task PushSyncCipherUpdateAsync(Cipher cipher, IEnumerable<Guid> collectionIds)
        => PushCipherAsync(cipher, PushType.SyncCipherUpdate, collectionIds);

    Task PushSyncCipherDeleteAsync(Cipher cipher)
        => PushCipherAsync(cipher, PushType.SyncCipherUpdate, null);

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
                Date = TimeProvider.GetUtcNow().UtcDateTime,
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
                Date = TimeProvider.GetUtcNow().UtcDateTime,
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
                Date = TimeProvider.GetUtcNow().UtcDateTime,
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
                Date = TimeProvider.GetUtcNow().UtcDateTime,
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
                Date = TimeProvider.GetUtcNow().UtcDateTime,
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
                Date = TimeProvider.GetUtcNow().UtcDateTime,
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
        Guid? installationId = notification.Global && InstallationId != Guid.Empty
            ? InstallationId
            : null;

        var message = new NotificationPushNotification
        {
            Id = notification.Id,
            Priority = notification.Priority,
            Global = notification.Global,
            ClientType = notification.ClientType,
            UserId = notification.UserId,
            OrganizationId = notification.OrganizationId,
            InstallationId = installationId,
            TaskId = notification.TaskId,
            Title = notification.Title,
            Body = notification.Body,
            CreationDate = notification.CreationDate,
            RevisionDate = notification.RevisionDate,
        };

        if (notification.Global)
        {
            if (installationId.HasValue)
            {
                return PushAsync(new PushNotification<NotificationPushNotification>
                {
                    Type = PushType.Notification,
                    Target = NotificationTarget.Installation,
                    TargetId = installationId.Value,
                    ExcludeCurrentContext = true,
                    Payload = message,
                    ClientType = notification.ClientType,
                });
            }
            else
            {
                Logger.LogWarning(
                    "Invalid global notification id {NotificationId} push notification. No installation id provided.",
                    notification.Id);
            }
        }
        else if (notification.UserId.HasValue)
        {
            return PushAsync(new PushNotification<NotificationPushNotification>
            {
                Type = PushType.Notification,
                Target = NotificationTarget.User,
                TargetId = notification.UserId.Value,
                Payload = message,
                ExcludeCurrentContext = true,
                ClientType = notification.ClientType,
            });
        }
        else if (notification.OrganizationId.HasValue)
        {
            return PushAsync(new PushNotification<NotificationPushNotification>
            {
                Type = PushType.Notification,
                Target = NotificationTarget.Organization,
                TargetId = notification.OrganizationId.Value,
                Payload = message,
                ExcludeCurrentContext = true,
                ClientType = notification.ClientType,
            });
        }
        else
        {
            Logger.LogWarning("Invalid notification id {NotificationId} push notification", notification.Id);
        }

        return Task.CompletedTask;
    }

    Task PushNotificationStatusAsync(Notification notification, NotificationStatus notificationStatus)
    {
        Guid? installationId = notification.Global && InstallationId != Guid.Empty
            ? InstallationId
            : null;

        var message = new NotificationPushNotification
        {
            Id = notification.Id,
            Priority = notification.Priority,
            Global = notification.Global,
            ClientType = notification.ClientType,
            UserId = notification.UserId,
            OrganizationId = notification.OrganizationId,
            InstallationId = installationId,
            TaskId = notification.TaskId,
            Title = notification.Title,
            Body = notification.Body,
            CreationDate = notification.CreationDate,
            RevisionDate = notification.RevisionDate,
            ReadDate = notificationStatus.ReadDate,
            DeletedDate = notificationStatus.DeletedDate,
        };

        if (notification.Global)
        {
            if (installationId.HasValue)
            {
                return PushAsync(new PushNotification<NotificationPushNotification>
                {
                    Type = PushType.NotificationStatus,
                    Target = NotificationTarget.Installation,
                    TargetId = installationId.Value,
                    ExcludeCurrentContext = true,
                    Payload = message,
                    ClientType = notification.ClientType,
                });
            }
            else
            {
                Logger.LogWarning(
                    "Invalid global notification status id {NotificationId} push notification. No installation id provided.",
                    notification.Id);
            }
        }
        else if (notification.UserId.HasValue)
        {
            return PushAsync(new PushNotification<NotificationPushNotification>
            {
                Type = PushType.NotificationStatus,
                Target = NotificationTarget.User,
                TargetId = notification.UserId.Value,
                Payload = message,
                ExcludeCurrentContext = true,
                ClientType = notification.ClientType,
            });
        }
        else if (notification.OrganizationId.HasValue)
        {
            return PushAsync(new PushNotification<NotificationPushNotification>
            {
                Type = PushType.NotificationStatus,
                Target = NotificationTarget.Organization,
                TargetId = notification.OrganizationId.Value,
                Payload = message,
                ExcludeCurrentContext = true,
                ClientType = notification.ClientType,
            });
        }
        else
        {
            Logger.LogWarning("Invalid notification status id {NotificationId} push notification", notification.Id);
        }

        return Task.CompletedTask;
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

    Task PushSyncOrganizationStatusAsync(Organization organization)
        => PushAsync(new PushNotification<OrganizationStatusPushNotification>
        {
            Type = PushType.SyncOrganizationStatusChanged,
            Target = NotificationTarget.Organization,
            TargetId = organization.Id,
            Payload = new OrganizationStatusPushNotification
            {
                OrganizationId = organization.Id,
                Enabled = organization.Enabled,
            },
            ExcludeCurrentContext = false,
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

    Task PushPendingSecurityTasksAsync(Guid userId)
        => PushAsync(new PushNotification<UserPushNotification>
        {
            Type = PushType.PendingSecurityTasks,
            Target = NotificationTarget.User,
            TargetId = userId,
            Payload = new UserPushNotification
            {
                UserId = userId,
                Date = TimeProvider.GetUtcNow().UtcDateTime,
            },
            ExcludeCurrentContext = false,
        });
#endregion

    Task PushCipherAsync(Cipher cipher, PushType pushType, IEnumerable<Guid>? collectionIds);

    Task PushAsync<T>(PushNotification<T> pushNotification)
        where T : class;
}
