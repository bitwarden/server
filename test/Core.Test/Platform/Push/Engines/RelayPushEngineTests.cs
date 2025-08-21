#nullable enable

using System.Text.Json.Nodes;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Entities;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.Platform.Push.Internal;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Tools.Entities;
using Bit.Core.Vault.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Bit.Core.Test.Platform.Push.Engines;

public class RelayPushNotificationServiceTests : PushTestBase
{
    private static readonly Guid _deviceId = Guid.Parse("c4730f80-caaa-4772-97bd-5c0d23a2baa3");
    private readonly IDeviceRepository _deviceRepository;

    public RelayPushNotificationServiceTests()
    {
        _deviceRepository = Substitute.For<IDeviceRepository>();

        _deviceRepository.GetByIdentifierAsync(DeviceIdentifier)
            .Returns(new Device
            {
                Id = _deviceId,
            });

        GlobalSettings.PushRelayBaseUri = "https://localhost:7777";
        GlobalSettings.Installation.Id = Guid.Parse("478c608a-99fd-452a-94f0-af271654e6ee");
        GlobalSettings.Installation.IdentityUri = "https://localhost:8888";
    }

    protected override IPushEngine CreateService()
    {
        return new RelayPushEngine(
            HttpClientFactory,
            _deviceRepository,
            GlobalSettings,
            HttpContextAccessor,
            NullLogger<RelayPushEngine>.Instance
        );
    }

    protected override string ExpectedClientUrl() => "https://localhost:7777/push/send";

    protected override JsonNode GetPushSyncCipherCreatePayload(Cipher cipher, Guid collectionIds)
    {
        return new JsonObject
        {
            ["UserId"] = cipher.UserId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = DeviceIdentifier,
            ["Type"] = 1,
            ["Payload"] = new JsonObject
            {
                ["Id"] = cipher.Id,
                ["UserId"] = cipher.UserId,
                ["OrganizationId"] = null,
                // Currently CollectionIds are not passed along from the method signature
                // to the request body. 
                ["CollectionIds"] = null,
                ["RevisionDate"] = cipher.RevisionDate,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };
    }

    protected override JsonNode GetPushSyncCipherUpdatePayload(Cipher cipher, Guid collectionIds)
    {
        return new JsonObject
        {
            ["UserId"] = cipher.UserId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = DeviceIdentifier,
            ["Type"] = 0,
            ["Payload"] = new JsonObject
            {
                ["Id"] = cipher.Id,
                ["UserId"] = cipher.UserId,
                ["OrganizationId"] = null,
                // Currently CollectionIds are not passed along from the method signature
                // to the request body. 
                ["CollectionIds"] = null,
                ["RevisionDate"] = cipher.RevisionDate,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };
    }

    protected override JsonNode GetPushSyncCipherDeletePayload(Cipher cipher)
    {
        return new JsonObject
        {
            ["UserId"] = cipher.UserId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = DeviceIdentifier,
            ["Type"] = 2,
            ["Payload"] = new JsonObject
            {
                ["Id"] = cipher.Id,
                ["UserId"] = cipher.UserId,
                ["OrganizationId"] = null,
                ["CollectionIds"] = null,
                ["RevisionDate"] = cipher.RevisionDate,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };
    }

    protected override JsonNode GetPushSyncFolderCreatePayload(Folder folder)
    {
        return new JsonObject
        {
            ["UserId"] = folder.UserId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = DeviceIdentifier,
            ["Type"] = 7,
            ["Payload"] = new JsonObject
            {
                ["Id"] = folder.Id,
                ["UserId"] = folder.UserId,
                ["RevisionDate"] = folder.RevisionDate,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };
    }

    protected override JsonNode GetPushSyncFolderUpdatePayload(Folder folder)
    {
        return new JsonObject
        {
            ["UserId"] = folder.UserId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = DeviceIdentifier,
            ["Type"] = 8,
            ["Payload"] = new JsonObject
            {
                ["Id"] = folder.Id,
                ["UserId"] = folder.UserId,
                ["RevisionDate"] = folder.RevisionDate,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };
    }

    protected override JsonNode GetPushSyncFolderDeletePayload(Folder folder)
    {
        return new JsonObject
        {
            ["UserId"] = folder.UserId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = DeviceIdentifier,
            ["Type"] = 3,
            ["Payload"] = new JsonObject
            {
                ["Id"] = folder.Id,
                ["UserId"] = folder.UserId,
                ["RevisionDate"] = folder.RevisionDate,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };
    }

    protected override JsonNode GetPushSyncCiphersPayload(Guid userId)
    {
        return new JsonObject
        {
            ["UserId"] = userId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = null,
            ["Type"] = 4,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = FakeTimeProvider.GetUtcNow().UtcDateTime,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };
    }

    protected override JsonNode GetPushSyncVaultPayload(Guid userId)
    {
        return new JsonObject
        {
            ["UserId"] = userId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = null,
            ["Type"] = 5,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = FakeTimeProvider.GetUtcNow().UtcDateTime,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };
    }

    protected override JsonNode GetPushSyncOrganizationsPayload(Guid userId)
    {
        return new JsonObject
        {
            ["UserId"] = userId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = null,
            ["Type"] = 17,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = FakeTimeProvider.GetUtcNow().UtcDateTime,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };
    }

    protected override JsonNode GetPushSyncOrgKeysPayload(Guid userId)
    {
        return new JsonObject
        {
            ["UserId"] = userId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = null,
            ["Type"] = 6,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = FakeTimeProvider.GetUtcNow().UtcDateTime,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };
    }

    protected override JsonNode GetPushSyncSettingsPayload(Guid userId)
    {
        return new JsonObject
        {
            ["UserId"] = userId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = null,
            ["Type"] = 10,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = FakeTimeProvider.GetUtcNow().UtcDateTime,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };
    }

    protected override JsonNode GetPushLogOutPayload(Guid userId, bool excludeCurrentContext)
    {
        JsonNode? identifier = excludeCurrentContext ? DeviceIdentifier : null;

        return new JsonObject
        {
            ["UserId"] = userId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = identifier,
            ["Type"] = 11,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = FakeTimeProvider.GetUtcNow().UtcDateTime,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };
    }
    protected override JsonNode GetPushSendCreatePayload(Send send)
    {
        return new JsonObject
        {
            ["UserId"] = send.UserId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = DeviceIdentifier,
            ["Type"] = 12,
            ["Payload"] = new JsonObject
            {
                ["Id"] = send.Id,
                ["UserId"] = send.UserId,
                ["RevisionDate"] = send.RevisionDate,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };
    }
    protected override JsonNode GetPushSendUpdatePayload(Send send)
    {
        return new JsonObject
        {
            ["UserId"] = send.UserId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = DeviceIdentifier,
            ["Type"] = 13,
            ["Payload"] = new JsonObject
            {
                ["Id"] = send.Id,
                ["UserId"] = send.UserId,
                ["RevisionDate"] = send.RevisionDate,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };
    }
    protected override JsonNode GetPushSendDeletePayload(Send send)
    {
        return new JsonObject
        {
            ["UserId"] = send.UserId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = DeviceIdentifier,
            ["Type"] = 14,
            ["Payload"] = new JsonObject
            {
                ["Id"] = send.Id,
                ["UserId"] = send.UserId,
                ["RevisionDate"] = send.RevisionDate,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };
    }
    protected override JsonNode GetPushAuthRequestPayload(AuthRequest authRequest)
    {
        return new JsonObject
        {
            ["UserId"] = authRequest.UserId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = DeviceIdentifier,
            ["Type"] = 15,
            ["Payload"] = new JsonObject
            {
                ["Id"] = authRequest.Id,
                ["UserId"] = authRequest.UserId,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };
    }
    protected override JsonNode GetPushAuthRequestResponsePayload(AuthRequest authRequest)
    {
        return new JsonObject
        {
            ["UserId"] = authRequest.UserId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = DeviceIdentifier,
            ["Type"] = 16,
            ["Payload"] = new JsonObject
            {
                ["Id"] = authRequest.Id,
                ["UserId"] = authRequest.UserId,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };
    }
    protected override JsonNode GetPushNotificationResponsePayload(Notification notification, Guid? userId, Guid? organizationId)
    {
        JsonNode? installationId = notification.Global ? GlobalSettings.Installation.Id : null;

        return new JsonObject
        {
            ["UserId"] = notification.UserId,
            ["OrganizationId"] = notification.OrganizationId,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = DeviceIdentifier,
            ["Type"] = 20,
            ["Payload"] = new JsonObject
            {
                ["Id"] = notification.Id,
                ["Priority"] = 3,
                ["Global"] = notification.Global,
                ["ClientType"] = 0,
                ["UserId"] = userId,
                ["OrganizationId"] = organizationId,
                ["TaskId"] = notification.TaskId,
                ["InstallationId"] = installationId,
                ["Title"] = notification.Title,
                ["Body"] = notification.Body,
                ["CreationDate"] = notification.CreationDate,
                ["RevisionDate"] = notification.RevisionDate,
                ["ReadDate"] = null,
                ["DeletedDate"] = null,
            },
            ["ClientType"] = 0,
            ["InstallationId"] = installationId?.DeepClone(),
        };
    }
    protected override JsonNode GetPushNotificationStatusResponsePayload(Notification notification, NotificationStatus notificationStatus, Guid? userId, Guid? organizationId)
    {
        JsonNode? installationId = notification.Global ? GlobalSettings.Installation.Id : null;

        return new JsonObject
        {
            ["UserId"] = notification.UserId,
            ["OrganizationId"] = notification.OrganizationId,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = DeviceIdentifier,
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
            ["ClientType"] = 0,
            ["InstallationId"] = installationId?.DeepClone(),
        };
    }
    protected override JsonNode GetPushSyncOrganizationStatusResponsePayload(Organization organization)
    {
        return new JsonObject
        {
            ["UserId"] = null,
            ["OrganizationId"] = organization.Id,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = null,
            ["Type"] = 18,
            ["Payload"] = new JsonObject
            {
                ["OrganizationId"] = organization.Id,
                ["Enabled"] = organization.Enabled,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };
    }
    protected override JsonNode GetPushSyncOrganizationCollectionManagementSettingsResponsePayload(Organization organization)
    {
        return new JsonObject
        {
            ["UserId"] = null,
            ["OrganizationId"] = organization.Id,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = null,
            ["Type"] = 19,
            ["Payload"] = new JsonObject
            {
                ["OrganizationId"] = organization.Id,
                ["LimitCollectionCreation"] = organization.LimitCollectionCreation,
                ["LimitCollectionDeletion"] = organization.LimitCollectionDeletion,
                ["LimitItemDeletion"] = organization.LimitItemDeletion,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };
    }

    protected override JsonNode GetPushRefreshSecurityTasksResponsePayload(Guid userId)
    {
        return new JsonObject
        {
            ["UserId"] = userId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = null,
            ["Type"] = 22,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = FakeTimeProvider.GetUtcNow().UtcDateTime,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };
    }
}
