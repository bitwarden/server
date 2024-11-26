#nullable enable
using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Models.Data;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationHub;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Test.NotificationCenter.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.NotificationHub;

[SutProviderCustomize]
[NotificationStatusCustomize]
public class NotificationHubPushNotificationServiceTests
{
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
