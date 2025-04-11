#nullable enable
using System.Text.Json;
using System.Text.Json.Nodes;
using Bit.Core.Auth.Entities;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Models.Data;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Enums;
using Bit.Core.NotificationHub;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Test.NotificationCenter.AutoFixture;
using Bit.Core.Tools.Entities;
using Bit.Core.Vault.Entities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.NotificationHub;

[SutProviderCustomize]
[NotificationStatusCustomize]
public class NotificationHubPushNotificationServiceTests
{
    private static readonly string _deviceIdentifier = "test_device_identifier";
    private static readonly DateTime _now = DateTime.UtcNow;
    private static readonly Guid _installationId = Guid.Parse("da73177b-513f-4444-b582-595c890e1022");

    [Theory]
    [BitAutoData]
    [NotificationCustomize]
    public async Task PushNotificationAsync_GlobalInstallationIdDefault_NotSent(
        SutProvider<NotificationHubPushNotificationService> sutProvider, Notification notification)
    {
        sutProvider.GetDependency<IGlobalSettings>().Installation.Id = default;

        await sutProvider.Sut.PushNotificationAsync(notification);

        await sutProvider.GetDependency<INotificationHubPool>()
            .Received(0)
            .AllClients
            .Received(0)
            .SendTemplateNotificationAsync(Arg.Any<IDictionary<string, string>>(), Arg.Any<string>());
        await sutProvider.GetDependency<IInstallationDeviceRepository>()
            .Received(0)
            .UpsertAsync(Arg.Any<InstallationDeviceEntity>());
    }

    [Theory]
    [BitAutoData]
    [NotificationCustomize]
    public async Task PushNotificationAsync_GlobalInstallationIdSetClientTypeAll_SentToInstallationId(
        SutProvider<NotificationHubPushNotificationService> sutProvider, Notification notification, Guid installationId)
    {
        sutProvider.GetDependency<IGlobalSettings>().Installation.Id = installationId;
        notification.ClientType = ClientType.All;
        var expectedNotification = ToNotificationPushNotification(notification, null, installationId);

        await sutProvider.Sut.PushNotificationAsync(notification);

        await AssertSendTemplateNotificationAsync(sutProvider, PushType.Notification,
            expectedNotification,
            $"(template:payload && installationId:{installationId})");
        await sutProvider.GetDependency<IInstallationDeviceRepository>()
            .Received(0)
            .UpsertAsync(Arg.Any<InstallationDeviceEntity>());
    }

    [Theory]
    [BitAutoData(ClientType.Browser)]
    [BitAutoData(ClientType.Desktop)]
    [BitAutoData(ClientType.Web)]
    [BitAutoData(ClientType.Mobile)]
    [NotificationCustomize]
    public async Task PushNotificationAsync_GlobalInstallationIdSetClientTypeNotAll_SentToInstallationIdAndClientType(
        ClientType clientType, SutProvider<NotificationHubPushNotificationService> sutProvider,
        Notification notification, Guid installationId)
    {
        sutProvider.GetDependency<IGlobalSettings>().Installation.Id = installationId;
        notification.ClientType = clientType;
        var expectedNotification = ToNotificationPushNotification(notification, null, installationId);

        await sutProvider.Sut.PushNotificationAsync(notification);

        await AssertSendTemplateNotificationAsync(sutProvider, PushType.Notification,
            expectedNotification,
            $"(template:payload && installationId:{installationId} && clientType:{clientType})");
        await sutProvider.GetDependency<IInstallationDeviceRepository>()
            .Received(0)
            .UpsertAsync(Arg.Any<InstallationDeviceEntity>());
    }

    [Theory]
    [BitAutoData(false)]
    [BitAutoData(true)]
    [NotificationCustomize(false)]
    public async Task PushNotificationAsync_UserIdProvidedClientTypeAll_SentToUser(
        bool organizationIdNull, SutProvider<NotificationHubPushNotificationService> sutProvider,
        Notification notification)
    {
        if (organizationIdNull)
        {
            notification.OrganizationId = null;
        }

        notification.ClientType = ClientType.All;
        var expectedNotification = ToNotificationPushNotification(notification, null, null);

        await sutProvider.Sut.PushNotificationAsync(notification);

        await AssertSendTemplateNotificationAsync(sutProvider, PushType.Notification,
            expectedNotification,
            $"(template:payload_userId:{notification.UserId})");
        await sutProvider.GetDependency<IInstallationDeviceRepository>()
            .Received(0)
            .UpsertAsync(Arg.Any<InstallationDeviceEntity>());
    }

    [Theory]
    [BitAutoData(ClientType.Browser)]
    [BitAutoData(ClientType.Desktop)]
    [BitAutoData(ClientType.Web)]
    [BitAutoData(ClientType.Mobile)]
    [NotificationCustomize(false)]
    public async Task PushNotificationAsync_UserIdProvidedOrganizationIdNullClientTypeNotAll_SentToUser(
        ClientType clientType, SutProvider<NotificationHubPushNotificationService> sutProvider,
        Notification notification)
    {
        notification.OrganizationId = null;
        notification.ClientType = clientType;
        var expectedNotification = ToNotificationPushNotification(notification, null, null);

        await sutProvider.Sut.PushNotificationAsync(notification);

        await AssertSendTemplateNotificationAsync(sutProvider, PushType.Notification,
            expectedNotification,
            $"(template:payload_userId:{notification.UserId} && clientType:{clientType})");
        await sutProvider.GetDependency<IInstallationDeviceRepository>()
            .Received(0)
            .UpsertAsync(Arg.Any<InstallationDeviceEntity>());
    }

    [Theory]
    [BitAutoData(ClientType.Browser)]
    [BitAutoData(ClientType.Desktop)]
    [BitAutoData(ClientType.Web)]
    [BitAutoData(ClientType.Mobile)]
    [NotificationCustomize(false)]
    public async Task PushNotificationAsync_UserIdProvidedOrganizationIdProvidedClientTypeNotAll_SentToUser(
        ClientType clientType, SutProvider<NotificationHubPushNotificationService> sutProvider,
        Notification notification)
    {
        notification.ClientType = clientType;
        var expectedNotification = ToNotificationPushNotification(notification, null, null);

        await sutProvider.Sut.PushNotificationAsync(notification);

        await AssertSendTemplateNotificationAsync(sutProvider, PushType.Notification,
            expectedNotification,
            $"(template:payload_userId:{notification.UserId} && clientType:{clientType})");
        await sutProvider.GetDependency<IInstallationDeviceRepository>()
            .Received(0)
            .UpsertAsync(Arg.Any<InstallationDeviceEntity>());
    }

    [Theory]
    [BitAutoData]
    [NotificationCustomize(false)]
    public async Task PushNotificationAsync_UserIdNullOrganizationIdProvidedClientTypeAll_SentToOrganization(
        SutProvider<NotificationHubPushNotificationService> sutProvider, Notification notification)
    {
        notification.UserId = null;
        notification.ClientType = ClientType.All;
        var expectedNotification = ToNotificationPushNotification(notification, null, null);

        await sutProvider.Sut.PushNotificationAsync(notification);

        await AssertSendTemplateNotificationAsync(sutProvider, PushType.Notification,
            expectedNotification,
            $"(template:payload && organizationId:{notification.OrganizationId})");
        await sutProvider.GetDependency<IInstallationDeviceRepository>()
            .Received(0)
            .UpsertAsync(Arg.Any<InstallationDeviceEntity>());
    }

    [Theory]
    [BitAutoData(ClientType.Browser)]
    [BitAutoData(ClientType.Desktop)]
    [BitAutoData(ClientType.Web)]
    [BitAutoData(ClientType.Mobile)]
    [NotificationCustomize(false)]
    public async Task PushNotificationAsync_UserIdNullOrganizationIdProvidedClientTypeNotAll_SentToOrganization(
        ClientType clientType, SutProvider<NotificationHubPushNotificationService> sutProvider,
        Notification notification)
    {
        notification.UserId = null;
        notification.ClientType = clientType;
        var expectedNotification = ToNotificationPushNotification(notification, null, null);

        await sutProvider.Sut.PushNotificationAsync(notification);

        await AssertSendTemplateNotificationAsync(sutProvider, PushType.Notification,
            expectedNotification,
            $"(template:payload && organizationId:{notification.OrganizationId} && clientType:{clientType})");
        await sutProvider.GetDependency<IInstallationDeviceRepository>()
            .Received(0)
            .UpsertAsync(Arg.Any<InstallationDeviceEntity>());
    }

    [Theory]
    [BitAutoData]
    [NotificationCustomize]
    public async Task PushNotificationStatusAsync_GlobalInstallationIdDefault_NotSent(
        SutProvider<NotificationHubPushNotificationService> sutProvider, Notification notification,
        NotificationStatus notificationStatus)
    {
        sutProvider.GetDependency<IGlobalSettings>().Installation.Id = default;

        await sutProvider.Sut.PushNotificationStatusAsync(notification, notificationStatus);

        await sutProvider.GetDependency<INotificationHubPool>()
            .Received(0)
            .AllClients
            .Received(0)
            .SendTemplateNotificationAsync(Arg.Any<IDictionary<string, string>>(), Arg.Any<string>());
        await sutProvider.GetDependency<IInstallationDeviceRepository>()
            .Received(0)
            .UpsertAsync(Arg.Any<InstallationDeviceEntity>());
    }

    [Theory]
    [BitAutoData]
    [NotificationCustomize]
    public async Task PushNotificationStatusAsync_GlobalInstallationIdSetClientTypeAll_SentToInstallationId(
        SutProvider<NotificationHubPushNotificationService> sutProvider,
        Notification notification, NotificationStatus notificationStatus, Guid installationId)
    {
        sutProvider.GetDependency<IGlobalSettings>().Installation.Id = installationId;
        notification.ClientType = ClientType.All;

        var expectedNotification = ToNotificationPushNotification(notification, notificationStatus, installationId);

        await sutProvider.Sut.PushNotificationStatusAsync(notification, notificationStatus);

        await AssertSendTemplateNotificationAsync(sutProvider, PushType.NotificationStatus,
            expectedNotification,
            $"(template:payload && installationId:{installationId})");
        await sutProvider.GetDependency<IInstallationDeviceRepository>()
            .Received(0)
            .UpsertAsync(Arg.Any<InstallationDeviceEntity>());
    }

    [Theory]
    [BitAutoData(ClientType.Browser)]
    [BitAutoData(ClientType.Desktop)]
    [BitAutoData(ClientType.Web)]
    [BitAutoData(ClientType.Mobile)]
    [NotificationCustomize]
    public async Task
        PushNotificationStatusAsync_GlobalInstallationIdSetClientTypeNotAll_SentToInstallationIdAndClientType(
            ClientType clientType, SutProvider<NotificationHubPushNotificationService> sutProvider,
            Notification notification, NotificationStatus notificationStatus, Guid installationId)
    {
        sutProvider.GetDependency<IGlobalSettings>().Installation.Id = installationId;
        notification.ClientType = clientType;

        var expectedNotification = ToNotificationPushNotification(notification, notificationStatus, installationId);

        await sutProvider.Sut.PushNotificationStatusAsync(notification, notificationStatus);

        await AssertSendTemplateNotificationAsync(sutProvider, PushType.NotificationStatus,
            expectedNotification,
            $"(template:payload && installationId:{installationId} && clientType:{clientType})");
        await sutProvider.GetDependency<IInstallationDeviceRepository>()
            .Received(0)
            .UpsertAsync(Arg.Any<InstallationDeviceEntity>());
    }

    [Theory]
    [BitAutoData(false)]
    [BitAutoData(true)]
    [NotificationCustomize(false)]
    public async Task PushNotificationStatusAsync_UserIdProvidedClientTypeAll_SentToUser(
        bool organizationIdNull, SutProvider<NotificationHubPushNotificationService> sutProvider,
        Notification notification, NotificationStatus notificationStatus)
    {
        if (organizationIdNull)
        {
            notification.OrganizationId = null;
        }

        notification.ClientType = ClientType.All;
        var expectedNotification = ToNotificationPushNotification(notification, notificationStatus, null);

        await sutProvider.Sut.PushNotificationStatusAsync(notification, notificationStatus);

        await AssertSendTemplateNotificationAsync(sutProvider, PushType.NotificationStatus,
            expectedNotification,
            $"(template:payload_userId:{notification.UserId})");
        await sutProvider.GetDependency<IInstallationDeviceRepository>()
            .Received(0)
            .UpsertAsync(Arg.Any<InstallationDeviceEntity>());
    }

    [Theory]
    [BitAutoData(ClientType.Browser)]
    [BitAutoData(ClientType.Desktop)]
    [BitAutoData(ClientType.Web)]
    [BitAutoData(ClientType.Mobile)]
    [NotificationCustomize(false)]
    public async Task PushNotificationStatusAsync_UserIdProvidedOrganizationIdNullClientTypeNotAll_SentToUser(
        ClientType clientType, SutProvider<NotificationHubPushNotificationService> sutProvider,
        Notification notification, NotificationStatus notificationStatus)
    {
        notification.OrganizationId = null;
        notification.ClientType = clientType;
        var expectedNotification = ToNotificationPushNotification(notification, notificationStatus, null);

        await sutProvider.Sut.PushNotificationStatusAsync(notification, notificationStatus);

        await AssertSendTemplateNotificationAsync(sutProvider, PushType.NotificationStatus,
            expectedNotification,
            $"(template:payload_userId:{notification.UserId} && clientType:{clientType})");
        await sutProvider.GetDependency<IInstallationDeviceRepository>()
            .Received(0)
            .UpsertAsync(Arg.Any<InstallationDeviceEntity>());
    }

    [Theory]
    [BitAutoData(ClientType.Browser)]
    [BitAutoData(ClientType.Desktop)]
    [BitAutoData(ClientType.Web)]
    [BitAutoData(ClientType.Mobile)]
    [NotificationCustomize(false)]
    public async Task PushNotificationStatusAsync_UserIdProvidedOrganizationIdProvidedClientTypeNotAll_SentToUser(
        ClientType clientType, SutProvider<NotificationHubPushNotificationService> sutProvider,
        Notification notification, NotificationStatus notificationStatus)
    {
        notification.ClientType = clientType;
        var expectedNotification = ToNotificationPushNotification(notification, notificationStatus, null);

        await sutProvider.Sut.PushNotificationStatusAsync(notification, notificationStatus);

        await AssertSendTemplateNotificationAsync(sutProvider, PushType.NotificationStatus,
            expectedNotification,
            $"(template:payload_userId:{notification.UserId} && clientType:{clientType})");
        await sutProvider.GetDependency<IInstallationDeviceRepository>()
            .Received(0)
            .UpsertAsync(Arg.Any<InstallationDeviceEntity>());
    }

    [Theory]
    [BitAutoData]
    [NotificationCustomize(false)]
    public async Task PushNotificationStatusAsync_UserIdNullOrganizationIdProvidedClientTypeAll_SentToOrganization(
        SutProvider<NotificationHubPushNotificationService> sutProvider, Notification notification,
        NotificationStatus notificationStatus)
    {
        notification.UserId = null;
        notification.ClientType = ClientType.All;
        var expectedNotification = ToNotificationPushNotification(notification, notificationStatus, null);

        await sutProvider.Sut.PushNotificationStatusAsync(notification, notificationStatus);

        await AssertSendTemplateNotificationAsync(sutProvider, PushType.NotificationStatus,
            expectedNotification,
            $"(template:payload && organizationId:{notification.OrganizationId})");
        await sutProvider.GetDependency<IInstallationDeviceRepository>()
            .Received(0)
            .UpsertAsync(Arg.Any<InstallationDeviceEntity>());
    }

    [Theory]
    [BitAutoData(ClientType.Browser)]
    [BitAutoData(ClientType.Desktop)]
    [BitAutoData(ClientType.Web)]
    [BitAutoData(ClientType.Mobile)]
    [NotificationCustomize(false)]
    public async Task
        PushNotificationStatusAsync_UserIdNullOrganizationIdProvidedClientTypeNotAll_SentToOrganization(
            ClientType clientType, SutProvider<NotificationHubPushNotificationService> sutProvider,
            Notification notification, NotificationStatus notificationStatus)
    {
        notification.UserId = null;
        notification.ClientType = clientType;
        var expectedNotification = ToNotificationPushNotification(notification, notificationStatus, null);

        await sutProvider.Sut.PushNotificationStatusAsync(notification, notificationStatus);

        await AssertSendTemplateNotificationAsync(sutProvider, PushType.NotificationStatus,
            expectedNotification,
            $"(template:payload && organizationId:{notification.OrganizationId} && clientType:{clientType})");
        await sutProvider.GetDependency<IInstallationDeviceRepository>()
            .Received(0)
            .UpsertAsync(Arg.Any<InstallationDeviceEntity>());
    }

    [Theory]
    [BitAutoData([null])]
    [BitAutoData(ClientType.All)]
    public async Task SendPayloadToUserAsync_ClientTypeNullOrAll_SentToUser(ClientType? clientType,
        SutProvider<NotificationHubPushNotificationService> sutProvider, Guid userId, PushType pushType, string payload,
        string identifier)
    {
        await sutProvider.Sut.SendPayloadToUserAsync(userId.ToString(), pushType, payload, identifier, null,
            clientType);

        await AssertSendTemplateNotificationAsync(sutProvider, pushType, payload,
            $"(template:payload_userId:{userId} && !deviceIdentifier:{identifier})");
        await sutProvider.GetDependency<IInstallationDeviceRepository>()
            .Received(0)
            .UpsertAsync(Arg.Any<InstallationDeviceEntity>());
    }

    [Theory]
    [BitAutoData(ClientType.Browser)]
    [BitAutoData(ClientType.Desktop)]
    [BitAutoData(ClientType.Mobile)]
    [BitAutoData(ClientType.Web)]
    public async Task SendPayloadToUserAsync_ClientTypeExplicit_SentToUserAndClientType(ClientType clientType,
        SutProvider<NotificationHubPushNotificationService> sutProvider, Guid userId, PushType pushType, string payload,
        string identifier)
    {
        await sutProvider.Sut.SendPayloadToUserAsync(userId.ToString(), pushType, payload, identifier, null,
            clientType);

        await AssertSendTemplateNotificationAsync(sutProvider, pushType, payload,
            $"(template:payload_userId:{userId} && !deviceIdentifier:{identifier} && clientType:{clientType})");
        await sutProvider.GetDependency<IInstallationDeviceRepository>()
            .Received(0)
            .UpsertAsync(Arg.Any<InstallationDeviceEntity>());
    }

    [Theory]
    [BitAutoData([null])]
    [BitAutoData(ClientType.All)]
    public async Task SendPayloadToOrganizationAsync_ClientTypeNullOrAll_SentToOrganization(ClientType? clientType,
        SutProvider<NotificationHubPushNotificationService> sutProvider, Guid organizationId, PushType pushType,
        string payload, string identifier)
    {
        await sutProvider.Sut.SendPayloadToOrganizationAsync(organizationId.ToString(), pushType, payload, identifier,
            null, clientType);

        await AssertSendTemplateNotificationAsync(sutProvider, pushType, payload,
            $"(template:payload && organizationId:{organizationId} && !deviceIdentifier:{identifier})");
        await sutProvider.GetDependency<IInstallationDeviceRepository>()
            .Received(0)
            .UpsertAsync(Arg.Any<InstallationDeviceEntity>());
    }

    [Theory]
    [BitAutoData(ClientType.Browser)]
    [BitAutoData(ClientType.Desktop)]
    [BitAutoData(ClientType.Mobile)]
    [BitAutoData(ClientType.Web)]
    public async Task SendPayloadToOrganizationAsync_ClientTypeExplicit_SentToOrganizationAndClientType(
        ClientType clientType, SutProvider<NotificationHubPushNotificationService> sutProvider, Guid organizationId,
        PushType pushType, string payload, string identifier)
    {
        await sutProvider.Sut.SendPayloadToOrganizationAsync(organizationId.ToString(), pushType, payload, identifier,
            null, clientType);

        await AssertSendTemplateNotificationAsync(sutProvider, pushType, payload,
            $"(template:payload && organizationId:{organizationId} && !deviceIdentifier:{identifier} && clientType:{clientType})");
        await sutProvider.GetDependency<IInstallationDeviceRepository>()
            .Received(0)
            .UpsertAsync(Arg.Any<InstallationDeviceEntity>());
    }

    [Theory]
    [BitAutoData([null])]
    [BitAutoData(ClientType.All)]
    public async Task SendPayloadToInstallationAsync_ClientTypeNullOrAll_SentToInstallation(ClientType? clientType,
        SutProvider<NotificationHubPushNotificationService> sutProvider, Guid installationId, PushType pushType,
        string payload, string identifier)
    {
        await sutProvider.Sut.SendPayloadToInstallationAsync(installationId.ToString(), pushType, payload, identifier,
            null, clientType);

        await AssertSendTemplateNotificationAsync(sutProvider, pushType, payload,
            $"(template:payload && installationId:{installationId} && !deviceIdentifier:{identifier})");
        await sutProvider.GetDependency<IInstallationDeviceRepository>()
            .Received(0)
            .UpsertAsync(Arg.Any<InstallationDeviceEntity>());
    }

    [Theory]
    [BitAutoData(ClientType.Browser)]
    [BitAutoData(ClientType.Desktop)]
    [BitAutoData(ClientType.Mobile)]
    [BitAutoData(ClientType.Web)]
    public async Task SendPayloadToInstallationAsync_ClientTypeExplicit_SentToInstallationAndClientType(
        ClientType clientType, SutProvider<NotificationHubPushNotificationService> sutProvider, Guid installationId,
        PushType pushType, string payload, string identifier)
    {
        await sutProvider.Sut.SendPayloadToInstallationAsync(installationId.ToString(), pushType, payload, identifier,
            null, clientType);

        await AssertSendTemplateNotificationAsync(sutProvider, pushType, payload,
            $"(template:payload && installationId:{installationId} && !deviceIdentifier:{identifier} && clientType:{clientType})");
        await sutProvider.GetDependency<IInstallationDeviceRepository>()
            .Received(0)
            .UpsertAsync(Arg.Any<InstallationDeviceEntity>());
    }

    [Fact]
    public async Task PushSyncCipherCreateAsync_SendExpectedData()
    {
        var collectionId = Guid.NewGuid();

        var userId = Guid.NewGuid();

        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RevisionDate = DateTime.UtcNow,
        };

        var expectedPayload = new JsonObject
        {
            ["Id"] = cipher.Id,
            ["UserId"] = cipher.UserId,
            ["OrganizationId"] = cipher.OrganizationId,
            ["CollectionIds"] = new JsonArray(collectionId),
            ["RevisionDate"] = cipher.RevisionDate,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncCipherCreateAsync(cipher, [collectionId]),
            PushType.SyncCipherCreate,
            expectedPayload,
            $"(template:payload_userId:{userId} && !deviceIdentifier:{_deviceIdentifier})"
        );
    }

    [Fact]
    public async Task PushSyncCipherUpdateAsync_SendExpectedData()
    {
        var collectionId = Guid.NewGuid();

        var userId = Guid.NewGuid();

        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RevisionDate = DateTime.UtcNow,
        };

        var expectedPayload = new JsonObject
        {
            ["Id"] = cipher.Id,
            ["UserId"] = cipher.UserId,
            ["OrganizationId"] = cipher.OrganizationId,
            ["CollectionIds"] = new JsonArray(collectionId),
            ["RevisionDate"] = cipher.RevisionDate,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncCipherUpdateAsync(cipher, [collectionId]),
            PushType.SyncCipherUpdate,
            expectedPayload,
            $"(template:payload_userId:{userId} && !deviceIdentifier:{_deviceIdentifier})"
        );
    }

    [Fact]
    public async Task PushSyncCipherDeleteAsync_SendExpectedData()
    {
        var userId = Guid.NewGuid();

        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RevisionDate = DateTime.UtcNow,
        };

        var expectedPayload = new JsonObject
        {
            ["Id"] = cipher.Id,
            ["UserId"] = cipher.UserId,
            ["OrganizationId"] = cipher.OrganizationId,
            ["CollectionIds"] = null,
            ["RevisionDate"] = cipher.RevisionDate,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncCipherDeleteAsync(cipher),
            PushType.SyncLoginDelete,
            expectedPayload,
            $"(template:payload_userId:{userId} && !deviceIdentifier:{_deviceIdentifier})"
        );
    }

    [Fact]
    public async Task PushSyncFolderCreateAsync_SendExpectedData()
    {
        var userId = Guid.NewGuid();

        var folder = new Folder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RevisionDate = DateTime.UtcNow,
        };

        var expectedPayload = new JsonObject
        {
            ["Id"] = folder.Id,
            ["UserId"] = folder.UserId,
            ["RevisionDate"] = folder.RevisionDate,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncFolderCreateAsync(folder),
            PushType.SyncFolderCreate,
            expectedPayload,
            $"(template:payload_userId:{userId} && !deviceIdentifier:{_deviceIdentifier})"
        );
    }

    [Fact]
    public async Task PushSyncFolderUpdateAsync_SendExpectedData()
    {
        var userId = Guid.NewGuid();

        var folder = new Folder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RevisionDate = DateTime.UtcNow,
        };

        var expectedPayload = new JsonObject
        {
            ["Id"] = folder.Id,
            ["UserId"] = folder.UserId,
            ["RevisionDate"] = folder.RevisionDate,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncFolderUpdateAsync(folder),
            PushType.SyncFolderUpdate,
            expectedPayload,
            $"(template:payload_userId:{userId} && !deviceIdentifier:{_deviceIdentifier})"
        );
    }

    [Fact]
    public async Task PushSyncSendCreateAsync_SendExpectedData()
    {
        var userId = Guid.NewGuid();

        var send = new Send
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RevisionDate = DateTime.UtcNow,
        };

        var expectedPayload = new JsonObject
        {
            ["Id"] = send.Id,
            ["UserId"] = send.UserId,
            ["RevisionDate"] = send.RevisionDate,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncSendCreateAsync(send),
            PushType.SyncSendCreate,
            expectedPayload,
            $"(template:payload_userId:{userId} && !deviceIdentifier:{_deviceIdentifier})"
        );
    }

    [Fact]
    public async Task PushAuthRequestAsync_SendExpectedData()
    {
        var userId = Guid.NewGuid();

        var authRequest = new AuthRequest
        {
            Id = Guid.NewGuid(),
            UserId = userId,
        };

        var expectedPayload = new JsonObject
        {
            ["Id"] = authRequest.Id,
            ["UserId"] = authRequest.UserId,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushAuthRequestAsync(authRequest),
            PushType.AuthRequest,
            expectedPayload,
            $"(template:payload_userId:{userId} && !deviceIdentifier:{_deviceIdentifier})"
        );
    }

    [Fact]
    public async Task PushAuthRequestResponseAsync_SendExpectedData()
    {
        var userId = Guid.NewGuid();

        var authRequest = new AuthRequest
        {
            Id = Guid.NewGuid(),
            UserId = userId,
        };

        var expectedPayload = new JsonObject
        {
            ["Id"] = authRequest.Id,
            ["UserId"] = authRequest.UserId,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushAuthRequestResponseAsync(authRequest),
            PushType.AuthRequestResponse,
            expectedPayload,
            $"(template:payload_userId:{userId} && !deviceIdentifier:{_deviceIdentifier})"
        );
    }

    [Fact]
    public async Task PushSyncSendUpdateAsync_SendExpectedData()
    {
        var userId = Guid.NewGuid();

        var send = new Send
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RevisionDate = DateTime.UtcNow,
        };

        var expectedPayload = new JsonObject
        {
            ["Id"] = send.Id,
            ["UserId"] = send.UserId,
            ["RevisionDate"] = send.RevisionDate,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncSendUpdateAsync(send),
            PushType.SyncSendUpdate,
            expectedPayload,
            $"(template:payload_userId:{userId} && !deviceIdentifier:{_deviceIdentifier})"
        );
    }

    [Fact]
    public async Task PushSyncSendDeleteAsync_SendExpectedData()
    {
        var userId = Guid.NewGuid();

        var send = new Send
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RevisionDate = DateTime.UtcNow,
        };

        var expectedPayload = new JsonObject
        {
            ["Id"] = send.Id,
            ["UserId"] = send.UserId,
            ["RevisionDate"] = send.RevisionDate,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncSendDeleteAsync(send),
            PushType.SyncSendDelete,
            expectedPayload,
            $"(template:payload_userId:{userId} && !deviceIdentifier:{_deviceIdentifier})"
        );
    }

    [Fact]
    public async Task PushSyncCiphersAsync_SendExpectedData()
    {
        var userId = Guid.NewGuid();

        var expectedPayload = new JsonObject
        {
            ["UserId"] = userId,
            ["Date"] = _now,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncCiphersAsync(userId),
            PushType.SyncCiphers,
            expectedPayload,
            $"(template:payload_userId:{userId})"
        );
    }

    [Fact]
    public async Task PushSyncVaultAsync_SendExpectedData()
    {
        var userId = Guid.NewGuid();

        var expectedPayload = new JsonObject
        {
            ["UserId"] = userId,
            ["Date"] = _now,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncVaultAsync(userId),
            PushType.SyncVault,
            expectedPayload,
            $"(template:payload_userId:{userId})"
        );
    }

    [Fact]
    public async Task PushSyncOrganizationsAsync_SendExpectedData()
    {
        var userId = Guid.NewGuid();

        var expectedPayload = new JsonObject
        {
            ["UserId"] = userId,
            ["Date"] = _now,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncOrganizationsAsync(userId),
            PushType.SyncOrganizations,
            expectedPayload,
            $"(template:payload_userId:{userId})"
        );
    }

    [Fact]
    public async Task PushSyncOrgKeysAsync_SendExpectedData()
    {
        var userId = Guid.NewGuid();

        var expectedPayload = new JsonObject
        {
            ["UserId"] = userId,
            ["Date"] = _now,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncOrgKeysAsync(userId),
            PushType.SyncOrgKeys,
            expectedPayload,
            $"(template:payload_userId:{userId})"
        );
    }

    [Fact]
    public async Task PushSyncSettingsAsync_SendExpectedData()
    {
        var userId = Guid.NewGuid();

        var expectedPayload = new JsonObject
        {
            ["UserId"] = userId,
            ["Date"] = _now,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncSettingsAsync(userId),
            PushType.SyncSettings,
            expectedPayload,
            $"(template:payload_userId:{userId})"
        );
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PushLogOutAsync_SendExpectedData(bool excludeCurrentContext)
    {
        var userId = Guid.NewGuid();

        var expectedPayload = new JsonObject
        {
            ["UserId"] = userId,
            ["Date"] = _now,
        };

        var expectedTag = excludeCurrentContext
            ? $"(template:payload_userId:{userId} && !deviceIdentifier:{_deviceIdentifier})"
            : $"(template:payload_userId:{userId})";

        await VerifyNotificationAsync(
            async sut => await sut.PushLogOutAsync(userId, excludeCurrentContext),
            PushType.LogOut,
            expectedPayload,
            expectedTag
        );
    }

    [Fact]
    public async Task PushSyncFolderDeleteAsync_SendExpectedData()
    {
        var userId = Guid.NewGuid();

        var folder = new Folder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RevisionDate = DateTime.UtcNow,
        };

        var expectedPayload = new JsonObject
        {
            ["Id"] = folder.Id,
            ["UserId"] = folder.UserId,
            ["RevisionDate"] = folder.RevisionDate,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncFolderDeleteAsync(folder),
            PushType.SyncFolderDelete,
            expectedPayload,
            $"(template:payload_userId:{userId} && !deviceIdentifier:{_deviceIdentifier})"
        );
    }

    [Theory]
    [InlineData(true, null, null)]
    [InlineData(false, "e8e08ce8-8a26-4a65-913a-ba1d8c478b2f", null)]
    [InlineData(false, null, "2f53ee32-edf9-4169-b276-760fe92e03bf")]
    public async Task PushNotificationAsync_SendExpectedData(bool global, string? userId, string? organizationId)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Priority = Priority.High,
            Global = global,
            ClientType = ClientType.All,
            UserId = userId != null ? Guid.Parse(userId) : null,
            OrganizationId = organizationId != null ? Guid.Parse(organizationId) : null,
            Title = "My Title",
            Body = "My Body",
            CreationDate = DateTime.UtcNow.AddDays(-1),
            RevisionDate = DateTime.UtcNow,
        };

        JsonNode? installationId = global ? _installationId : null;

        var expectedPayload = new JsonObject
        {
            ["Id"] = notification.Id,
            ["Priority"] = 3,
            ["Global"] = global,
            ["ClientType"] = 0,
            ["UserId"] = notification.UserId,
            ["OrganizationId"] = notification.OrganizationId,
            ["InstallationId"] = installationId,
            ["Title"] = notification.Title,
            ["Body"] = notification.Body,
            ["CreationDate"] = notification.CreationDate,
            ["RevisionDate"] = notification.RevisionDate,
            ["ReadDate"] = null,
            ["DeletedDate"] = null,
        };

        string expectedTag;

        if (global)
        {
            expectedTag = $"(template:payload && installationId:{_installationId} && !deviceIdentifier:{_deviceIdentifier})";
        }
        else if (notification.OrganizationId.HasValue)
        {
            expectedTag = "(template:payload && organizationId:2f53ee32-edf9-4169-b276-760fe92e03bf && !deviceIdentifier:test_device_identifier)";
        }
        else
        {
            expectedTag = $"(template:payload_userId:{userId} && !deviceIdentifier:{_deviceIdentifier})";
        }

        await VerifyNotificationAsync(
            async sut => await sut.PushNotificationAsync(notification),
            PushType.Notification,
            expectedPayload,
            expectedTag
        );
    }

    [Theory]
    [InlineData(true, null, null)]
    [InlineData(false, "e8e08ce8-8a26-4a65-913a-ba1d8c478b2f", null)]
    [InlineData(false, null, "2f53ee32-edf9-4169-b276-760fe92e03bf")]
    public async Task PushNotificationStatusAsync_SendExpectedData(bool global, string? userId, string? organizationId)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Priority = Priority.High,
            Global = global,
            ClientType = ClientType.All,
            UserId = userId != null ? Guid.Parse(userId) : null,
            OrganizationId = organizationId != null ? Guid.Parse(organizationId) : null,
            Title = "My Title",
            Body = "My Body",
            CreationDate = DateTime.UtcNow.AddDays(-1),
            RevisionDate = DateTime.UtcNow,
        };

        var notificationStatus = new NotificationStatus
        {
            ReadDate = DateTime.UtcNow.AddDays(-1),
            DeletedDate = DateTime.UtcNow,
        };

        JsonNode? installationId = global ? _installationId : null;

        var expectedPayload = new JsonObject
        {
            ["Id"] = notification.Id,
            ["Priority"] = 3,
            ["Global"] = global,
            ["ClientType"] = 0,
            ["UserId"] = notification.UserId,
            ["OrganizationId"] = notification.OrganizationId,
            ["InstallationId"] = installationId,
            ["Title"] = notification.Title,
            ["Body"] = notification.Body,
            ["CreationDate"] = notification.CreationDate,
            ["RevisionDate"] = notification.RevisionDate,
            ["ReadDate"] = notificationStatus.ReadDate,
            ["DeletedDate"] = notificationStatus.DeletedDate,
        };

        string expectedTag;

        if (global)
        {
            expectedTag = $"(template:payload && installationId:{_installationId} && !deviceIdentifier:{_deviceIdentifier})";
        }
        else if (notification.OrganizationId.HasValue)
        {
            expectedTag = "(template:payload && organizationId:2f53ee32-edf9-4169-b276-760fe92e03bf && !deviceIdentifier:test_device_identifier)";
        }
        else
        {
            expectedTag = $"(template:payload_userId:{userId} && !deviceIdentifier:{_deviceIdentifier})";
        }

        await VerifyNotificationAsync(
            async sut => await sut.PushNotificationStatusAsync(notification, notificationStatus),
            PushType.NotificationStatus,
            expectedPayload,
            expectedTag
        );
    }

    private async Task VerifyNotificationAsync(Func<NotificationHubPushNotificationService, Task> test,
        PushType type, JsonNode expectedPayload, string tag)
    {
        var installationDeviceRepository = Substitute.For<IInstallationDeviceRepository>();

        var notificationHubPool = Substitute.For<INotificationHubPool>();

        var notificationHubProxy = Substitute.For<INotificationHubProxy>();

        notificationHubPool.AllClients
            .Returns(notificationHubProxy);

        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();

        var httpContext = new DefaultHttpContext();

        var serviceCollection = new ServiceCollection();
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.DeviceIdentifier = _deviceIdentifier;
        serviceCollection.AddSingleton(currentContext);

        httpContext.RequestServices = serviceCollection.BuildServiceProvider();

        httpContextAccessor.HttpContext
            .Returns(httpContext);

        var globalSettings = new Core.Settings.GlobalSettings();
        globalSettings.Installation.Id = _installationId;

        var fakeTimeProvider = new FakeTimeProvider();

        fakeTimeProvider.SetUtcNow(_now);

        var sut = new NotificationHubPushNotificationService(
            installationDeviceRepository,
            notificationHubPool,
            httpContextAccessor,
            NullLogger<NotificationHubPushNotificationService>.Instance,
            globalSettings,
            fakeTimeProvider
        );

        // Act
        await test(sut);

        // Assert
        var calls = notificationHubProxy.ReceivedCalls();
        var methodInfo = typeof(INotificationHubProxy).GetMethod(nameof(INotificationHubProxy.SendTemplateNotificationAsync));
        var call = Assert.Single(calls, c => c.GetMethodInfo() == methodInfo);

        var arguments = call.GetArguments();

        var dictionaryArg = (Dictionary<string, string>)arguments[0]!;
        var tagArg = (string)arguments[1]!;

        Assert.Equal(2, dictionaryArg.Count);
        Assert.True(dictionaryArg.TryGetValue("type", out var typeString));
        Assert.True(byte.TryParse(typeString, out var typeByte));
        Assert.Equal(type, (PushType)typeByte);

        Assert.True(dictionaryArg.TryGetValue("payload", out var payloadString));
        var actualPayloadNode = JsonNode.Parse(payloadString);

        Assert.True(JsonNode.DeepEquals(expectedPayload, actualPayloadNode));

        Assert.Equal(tag, tagArg);
    }

    private static NotificationPushNotification ToNotificationPushNotification(Notification notification,
        NotificationStatus? notificationStatus, Guid? installationId) =>
        new()
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
            ReadDate = notificationStatus?.ReadDate,
            DeletedDate = notificationStatus?.DeletedDate
        };

    private static async Task AssertSendTemplateNotificationAsync(
        SutProvider<NotificationHubPushNotificationService> sutProvider, PushType type, object payload, string tag)
    {
        await sutProvider.GetDependency<INotificationHubPool>()
            .Received(1)
            .AllClients
            .Received(1)
            .SendTemplateNotificationAsync(
                Arg.Is<IDictionary<string, string>>(dictionary => MatchingSendPayload(dictionary, type, payload)),
                tag);
    }

    private static bool MatchingSendPayload(IDictionary<string, string> dictionary, PushType type, object payload)
    {
        return dictionary.ContainsKey("type") && dictionary["type"].Equals(((byte)type).ToString()) &&
               dictionary.ContainsKey("payload") && dictionary["payload"].Equals(JsonSerializer.Serialize(payload));
    }
}
