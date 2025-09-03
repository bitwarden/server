using System.Text.Json.Nodes;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.Platform.Push.Internal;
using Bit.Core.Tools.Entities;
using Bit.Core.Vault.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bit.Core.Test.Platform.Push.Engines;

public class NotificationsApiPushEngineTests : PushTestBase
{
    public NotificationsApiPushEngineTests()
    {
        GlobalSettings.BaseServiceUri.InternalNotifications = "https://localhost:7777";
        GlobalSettings.BaseServiceUri.InternalIdentity = "https://localhost:8888";
    }

    protected override string ExpectedClientUrl() => "https://localhost:7777/send";

    protected override IPushEngine CreateService()
    {
        return new NotificationsApiPushEngine(
            HttpClientFactory,
            GlobalSettings,
            HttpContextAccessor,
            NullLogger<NotificationsApiPushEngine>.Instance
        );
    }

    protected override JsonNode GetPushSyncCipherCreatePayload(Cipher cipher, Guid collectionId)
    {
        return new JsonObject
        {
            ["Type"] = 1,
            ["Payload"] = new JsonObject
            {
                ["Id"] = cipher.Id,
                ["UserId"] = cipher.UserId,
                ["OrganizationId"] = null,
                ["CollectionIds"] = new JsonArray(collectionId),
                ["RevisionDate"] = cipher.RevisionDate,
            },
            ["ContextId"] = DeviceIdentifier,
        };
    }
    protected override JsonNode GetPushSyncCipherUpdatePayload(Cipher cipher, Guid collectionId)
    {
        return new JsonObject
        {
            ["Type"] = 0,
            ["Payload"] = new JsonObject
            {
                ["Id"] = cipher.Id,
                ["UserId"] = cipher.UserId,
                ["OrganizationId"] = null,
                ["CollectionIds"] = new JsonArray(collectionId),
                ["RevisionDate"] = cipher.RevisionDate,
            },
            ["ContextId"] = DeviceIdentifier,
        };
    }
    protected override JsonNode GetPushSyncCipherDeletePayload(Cipher cipher)
    {
        return new JsonObject
        {
            ["Type"] = 2,
            ["Payload"] = new JsonObject
            {
                ["Id"] = cipher.Id,
                ["UserId"] = cipher.UserId,
                ["OrganizationId"] = null,
                ["CollectionIds"] = null,
                ["RevisionDate"] = cipher.RevisionDate,
            },
            ["ContextId"] = DeviceIdentifier,
        };
    }

    protected override JsonNode GetPushSyncFolderCreatePayload(Folder folder)
    {
        return new JsonObject
        {
            ["Type"] = 7,
            ["Payload"] = new JsonObject
            {
                ["Id"] = folder.Id,
                ["UserId"] = folder.UserId,
                ["RevisionDate"] = folder.RevisionDate,
            },
            ["ContextId"] = DeviceIdentifier,
        };
    }

    protected override JsonNode GetPushSyncFolderUpdatePayload(Folder folder)
    {
        return new JsonObject
        {
            ["Type"] = 8,
            ["Payload"] = new JsonObject
            {
                ["Id"] = folder.Id,
                ["UserId"] = folder.UserId,
                ["RevisionDate"] = folder.RevisionDate,
            },
            ["ContextId"] = DeviceIdentifier,
        };
    }

    protected override JsonNode GetPushSyncFolderDeletePayload(Folder folder)
    {
        return new JsonObject
        {
            ["Type"] = 3,
            ["Payload"] = new JsonObject
            {
                ["Id"] = folder.Id,
                ["UserId"] = folder.UserId,
                ["RevisionDate"] = folder.RevisionDate,
            },
            ["ContextId"] = DeviceIdentifier,
        };
    }

    protected override JsonNode GetPushSyncCiphersPayload(Guid userId)
    {
        return new JsonObject
        {
            ["Type"] = 4,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = FakeTimeProvider.GetUtcNow().UtcDateTime,
            },
            ["ContextId"] = null,
        };
    }

    protected override JsonNode GetPushSyncVaultPayload(Guid userId)
    {
        return new JsonObject
        {
            ["Type"] = 5,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = FakeTimeProvider.GetUtcNow().UtcDateTime,
            },
            ["ContextId"] = null,
        };
    }

    protected override JsonNode GetPushSyncOrganizationsPayload(Guid userId)
    {
        return new JsonObject
        {
            ["Type"] = 17,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = FakeTimeProvider.GetUtcNow().UtcDateTime,
            },
            ["ContextId"] = null,
        };
    }

    protected override JsonNode GetPushSyncOrgKeysPayload(Guid userId)
    {
        return new JsonObject
        {
            ["Type"] = 6,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = FakeTimeProvider.GetUtcNow().UtcDateTime,
            },
            ["ContextId"] = null,
        };
    }

    protected override JsonNode GetPushSyncSettingsPayload(Guid userId)
    {
        return new JsonObject
        {
            ["Type"] = 10,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = FakeTimeProvider.GetUtcNow().UtcDateTime,
            },
            ["ContextId"] = null,
        };
    }

    protected override JsonNode GetPushLogOutPayload(Guid userId, bool excludeCurrentContext)
    {
        JsonNode? contextId = excludeCurrentContext ? DeviceIdentifier : null;

        return new JsonObject
        {
            ["Type"] = 11,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = FakeTimeProvider.GetUtcNow().UtcDateTime,
            },
            ["ContextId"] = contextId,
        };
    }

    protected override JsonNode GetPushSendCreatePayload(Send send)
    {
        return new JsonObject
        {
            ["Type"] = 12,
            ["Payload"] = new JsonObject
            {
                ["Id"] = send.Id,
                ["UserId"] = send.UserId,
                ["RevisionDate"] = send.RevisionDate,
            },
            ["ContextId"] = DeviceIdentifier,
        };
    }

    protected override JsonNode GetPushSendUpdatePayload(Send send)
    {
        return new JsonObject
        {
            ["Type"] = 13,
            ["Payload"] = new JsonObject
            {
                ["Id"] = send.Id,
                ["UserId"] = send.UserId,
                ["RevisionDate"] = send.RevisionDate,
            },
            ["ContextId"] = DeviceIdentifier,
        };
    }

    protected override JsonNode GetPushSendDeletePayload(Send send)
    {
        return new JsonObject
        {
            ["Type"] = 14,
            ["Payload"] = new JsonObject
            {
                ["Id"] = send.Id,
                ["UserId"] = send.UserId,
                ["RevisionDate"] = send.RevisionDate,
            },
            ["ContextId"] = DeviceIdentifier,
        };
    }

    protected override JsonNode GetPushAuthRequestPayload(AuthRequest authRequest)
    {
        return new JsonObject
        {
            ["Type"] = 15,
            ["Payload"] = new JsonObject
            {
                ["Id"] = authRequest.Id,
                ["UserId"] = authRequest.UserId,
            },
            ["ContextId"] = DeviceIdentifier,
        };
    }

    protected override JsonNode GetPushAuthRequestResponsePayload(AuthRequest authRequest)
    {
        return new JsonObject
        {
            ["Type"] = 16,
            ["Payload"] = new JsonObject
            {
                ["Id"] = authRequest.Id,
                ["UserId"] = authRequest.UserId,
            },
            ["ContextId"] = DeviceIdentifier,
        };
    }

    protected override JsonNode GetPushNotificationResponsePayload(Notification notification, Guid? userId, Guid? organizationId)
    {
        JsonNode? installationId = notification.Global ? GlobalSettings.Installation.Id : null;

        return new JsonObject
        {
            ["Type"] = 20,
            ["Payload"] = new JsonObject
            {
                ["Id"] = notification.Id,
                ["Priority"] = 3,
                ["Global"] = notification.Global,
                ["ClientType"] = 0,
                ["UserId"] = notification.UserId,
                ["OrganizationId"] = notification.OrganizationId,
                ["TaskId"] = notification.TaskId,
                ["InstallationId"] = installationId,
                ["Title"] = notification.Title,
                ["Body"] = notification.Body,
                ["CreationDate"] = notification.CreationDate,
                ["RevisionDate"] = notification.RevisionDate,
                ["ReadDate"] = null,
                ["DeletedDate"] = null,
            },
            ["ContextId"] = DeviceIdentifier,
        };
    }

    protected override JsonNode GetPushNotificationStatusResponsePayload(Notification notification, NotificationStatus notificationStatus, Guid? userId, Guid? organizationId)
    {
        JsonNode? installationId = notification.Global ? GlobalSettings.Installation.Id : null;

        return new JsonObject
        {
            ["Type"] = 21,
            ["Payload"] = new JsonObject
            {
                ["Id"] = notification.Id,
                ["Priority"] = 3,
                ["Global"] = notification.Global,
                ["ClientType"] = 0,
                ["UserId"] = notification.UserId,
                ["OrganizationId"] = notification.OrganizationId,
                ["InstallationId"] = installationId,
                ["TaskId"] = notification.TaskId,
                ["Title"] = notification.Title,
                ["Body"] = notification.Body,
                ["CreationDate"] = notification.CreationDate,
                ["RevisionDate"] = notification.RevisionDate,
                ["ReadDate"] = notificationStatus.ReadDate,
                ["DeletedDate"] = notificationStatus.DeletedDate,
            },
            ["ContextId"] = DeviceIdentifier,
        };
    }

    protected override JsonNode GetPushSyncOrganizationStatusResponsePayload(Organization organization)
    {
        return new JsonObject
        {
            ["Type"] = 18,
            ["Payload"] = new JsonObject
            {
                ["OrganizationId"] = organization.Id,
                ["Enabled"] = organization.Enabled,
            },
            ["ContextId"] = null,
        };
    }

    protected override JsonNode GetPushSyncOrganizationCollectionManagementSettingsResponsePayload(Organization organization)
    {
        return new JsonObject
        {
            ["Type"] = 19,
            ["Payload"] = new JsonObject
            {
                ["OrganizationId"] = organization.Id,
                ["LimitCollectionCreation"] = organization.LimitCollectionCreation,
                ["LimitCollectionDeletion"] = organization.LimitCollectionDeletion,
                ["LimitItemDeletion"] = organization.LimitItemDeletion,
            },
            ["ContextId"] = null,
        };
    }

    protected override JsonNode GetPushRefreshSecurityTasksResponsePayload(Guid userId)
    {
        return new JsonObject
        {
            ["Type"] = 22,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = FakeTimeProvider.GetUtcNow().UtcDateTime,
            },
            ["ContextId"] = null,
        };
    }
}
