#nullable enable
using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Models.Data;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationHub;
using Bit.Core.Repositories;
using Bit.Core.Test.NotificationCenter.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.NotificationHub;

[SutProviderCustomize]
public class NotificationHubPushNotificationServiceTests
{
    [Theory]
    [BitAutoData]
    [NotificationCustomize]
    public async void PushSyncNotificationAsync_Global_NotSent(
        SutProvider<NotificationHubPushNotificationService> sutProvider, Notification notification)
    {
        await sutProvider.Sut.PushSyncNotificationAsync(notification);

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
    [BitAutoData(false)]
    [BitAutoData(true)]
    [NotificationCustomize(false)]
    public async void PushSyncNotificationAsync_UserIdProvidedClientTypeAll_SentToUser(
        bool organizationIdNull, SutProvider<NotificationHubPushNotificationService> sutProvider,
        Notification notification)
    {
        if (organizationIdNull)
        {
            notification.OrganizationId = null;
        }

        notification.ClientType = ClientType.All;
        var expectedSyncNotification = ToSyncNotificationPushNotification(notification);

        await sutProvider.Sut.PushSyncNotificationAsync(notification);

        await AssertSendTemplateNotificationAsync(sutProvider, PushType.SyncNotification, expectedSyncNotification,
            $"(template:payload_userId:{notification.UserId})");
        await sutProvider.GetDependency<IInstallationDeviceRepository>()
            .Received(0)
            .UpsertAsync(Arg.Any<InstallationDeviceEntity>());
    }

    [Theory]
    [BitAutoData(false, ClientType.Browser)]
    [BitAutoData(false, ClientType.Desktop)]
    [BitAutoData(false, ClientType.Web)]
    [BitAutoData(false, ClientType.Mobile)]
    [BitAutoData(true, ClientType.Browser)]
    [BitAutoData(true, ClientType.Desktop)]
    [BitAutoData(true, ClientType.Web)]
    [BitAutoData(true, ClientType.Mobile)]
    [NotificationCustomize(false)]
    public async void PushSyncNotificationAsync_UserIdProvidedClientTypeNotAll_SentToUser(bool organizationIdNull,
        ClientType clientType, SutProvider<NotificationHubPushNotificationService> sutProvider,
        Notification notification)
    {
        if (organizationIdNull)
        {
            notification.OrganizationId = null;
        }

        notification.ClientType = clientType;
        var expectedSyncNotification = ToSyncNotificationPushNotification(notification);

        await sutProvider.Sut.PushSyncNotificationAsync(notification);

        await AssertSendTemplateNotificationAsync(sutProvider, PushType.SyncNotification, expectedSyncNotification,
            $"(template:payload_userId:{notification.UserId} && clientType:{clientType})");
        await sutProvider.GetDependency<IInstallationDeviceRepository>()
            .Received(0)
            .UpsertAsync(Arg.Any<InstallationDeviceEntity>());
    }

    [Theory]
    [BitAutoData]
    [NotificationCustomize(false)]
    public async void PushSyncNotificationAsync_UserIdNullOrganizationIdProvidedClientTypeAll_SentToOrganization(
        SutProvider<NotificationHubPushNotificationService> sutProvider, Notification notification)
    {
        notification.UserId = null;
        notification.ClientType = ClientType.All;
        var expectedSyncNotification = ToSyncNotificationPushNotification(notification);

        await sutProvider.Sut.PushSyncNotificationAsync(notification);

        await AssertSendTemplateNotificationAsync(sutProvider, PushType.SyncNotification, expectedSyncNotification,
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
    public async void PushSyncNotificationAsync_UserIdNullOrganizationIdProvidedClientTypeNotAll_SentToOrganization(
        ClientType clientType, SutProvider<NotificationHubPushNotificationService> sutProvider,
        Notification notification)
    {
        notification.UserId = null;
        notification.ClientType = clientType;

        var expectedSyncNotification = ToSyncNotificationPushNotification(notification);

        await sutProvider.Sut.PushSyncNotificationAsync(notification);

        await AssertSendTemplateNotificationAsync(sutProvider, PushType.SyncNotification, expectedSyncNotification,
            $"(template:payload && organizationId:{notification.OrganizationId} && clientType:{clientType})");
        await sutProvider.GetDependency<IInstallationDeviceRepository>()
            .Received(0)
            .UpsertAsync(Arg.Any<InstallationDeviceEntity>());
    }

    [Theory]
    [BitAutoData([null])]
    [BitAutoData(ClientType.All)]
    public async void SendPayloadToUserAsync_ClientTypeNullOrAll_SentToUser(ClientType? clientType,
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
    public async void SendPayloadToUserAsync_ClientTypeExplicit_SentToUserAndClientType(ClientType clientType,
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
    public async void SendPayloadToOrganizationAsync_ClientTypeNullOrAll_SentToOrganization(ClientType? clientType,
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
    public async void SendPayloadToOrganizationAsync_ClientTypeExplicit_SentToOrganizationAndClientType(
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

    private static SyncNotificationPushNotification ToSyncNotificationPushNotification(Notification notification) =>
        new()
        {
            Id = notification.Id,
            UserId = notification.UserId,
            OrganizationId = notification.OrganizationId,
            ClientType = notification.ClientType,
            RevisionDate = notification.RevisionDate
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
