#nullable enable
using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Test.NotificationCenter.AutoFixture;
using Bit.Core.Utilities;
using Bit.Notifications;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace Notifications.Test;

[SutProviderCustomize]
[NotificationCustomize(false)]
public class HubHelpersTest
{
    [Theory]
    [BitAutoData]
    public async Task SendNotificationToHubAsync_NotificationPushNotificationGlobal_NothingSent(
        SutProvider<HubHelpers> sutProvider,
        NotificationPushNotification notification,
        string contextId, CancellationToken cancellationToke)
    {
        notification.Global = true;
        notification.InstallationId = null;
        notification.UserId = null;
        notification.OrganizationId = null;

        var json = ToNotificationJson(notification, PushType.Notification, contextId);
        await sutProvider.Sut.SendNotificationToHubAsync(json, cancellationToke);

        sutProvider.GetDependency<IHubContext<NotificationsHub>>().Clients.Received(0).User(Arg.Any<string>());
        sutProvider.GetDependency<IHubContext<NotificationsHub>>().Clients.Received(0).Group(Arg.Any<string>());
        sutProvider.GetDependency<IHubContext<AnonymousNotificationsHub>>().Clients.Received(0).User(Arg.Any<string>());
        sutProvider.GetDependency<IHubContext<AnonymousNotificationsHub>>().Clients.Received(0)
            .Group(Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    public async Task
        SendNotificationToHubAsync_NotificationPushNotificationInstallationIdProvidedClientTypeAll_SentToGroupInstallation(
            SutProvider<HubHelpers> sutProvider,
            NotificationPushNotification notification,
            string contextId, CancellationToken cancellationToken)
    {
        notification.UserId = null;
        notification.OrganizationId = null;
        notification.ClientType = ClientType.All;

        var json = ToNotificationJson(notification, PushType.Notification, contextId);
        await sutProvider.Sut.SendNotificationToHubAsync(json, cancellationToken);

        sutProvider.GetDependency<IHubContext<NotificationsHub>>().Clients.Received(0).User(Arg.Any<string>());
        await sutProvider.GetDependency<IHubContext<NotificationsHub>>().Clients.Received(1)
            .Group($"Installation_{notification.InstallationId!.Value.ToString()}")
            .Received(1)
            .SendCoreAsync("ReceiveMessage", Arg.Is<object?[]>(objects =>
                    objects.Length == 1 && IsNotificationPushNotificationEqual(notification, objects[0],
                        PushType.Notification, contextId)),
                cancellationToken);
        sutProvider.GetDependency<IHubContext<AnonymousNotificationsHub>>().Clients.Received(0).User(Arg.Any<string>());
        sutProvider.GetDependency<IHubContext<AnonymousNotificationsHub>>().Clients.Received(0)
            .Group(Arg.Any<string>());
    }

    [Theory]
    [BitAutoData(ClientType.Browser)]
    [BitAutoData(ClientType.Desktop)]
    [BitAutoData(ClientType.Mobile)]
    [BitAutoData(ClientType.Web)]
    public async Task
        SendNotificationToHubAsync_NotificationPushNotificationInstallationIdProvidedClientTypeNotAll_SentToGroupInstallationClientType(
            ClientType clientType, SutProvider<HubHelpers> sutProvider,
            NotificationPushNotification notification,
            string contextId, CancellationToken cancellationToken)
    {
        notification.UserId = null;
        notification.OrganizationId = null;
        notification.ClientType = clientType;

        var json = ToNotificationJson(notification, PushType.Notification, contextId);
        await sutProvider.Sut.SendNotificationToHubAsync(json, cancellationToken);

        sutProvider.GetDependency<IHubContext<NotificationsHub>>().Clients.Received(0).User(Arg.Any<string>());
        await sutProvider.GetDependency<IHubContext<NotificationsHub>>().Clients.Received(1)
            .Group($"Installation_ClientType_{notification.InstallationId!.Value}_{clientType}")
            .Received(1)
            .SendCoreAsync("ReceiveMessage", Arg.Is<object?[]>(objects =>
                    objects.Length == 1 && IsNotificationPushNotificationEqual(notification, objects[0],
                        PushType.Notification, contextId)),
                cancellationToken);
        sutProvider.GetDependency<IHubContext<AnonymousNotificationsHub>>().Clients.Received(0).User(Arg.Any<string>());
        sutProvider.GetDependency<IHubContext<AnonymousNotificationsHub>>().Clients.Received(0)
            .Group(Arg.Any<string>());
    }

    [Theory]
    [BitAutoData(false)]
    [BitAutoData(true)]
    public async Task SendNotificationToHubAsync_NotificationPushNotificationUserIdProvidedClientTypeAll_SentToUser(
        bool organizationIdProvided, SutProvider<HubHelpers> sutProvider,
        NotificationPushNotification notification,
        string contextId, CancellationToken cancellationToken)
    {
        notification.InstallationId = null;
        notification.ClientType = ClientType.All;
        if (!organizationIdProvided)
        {
            notification.OrganizationId = null;
        }

        var json = ToNotificationJson(notification, PushType.Notification, contextId);
        await sutProvider.Sut.SendNotificationToHubAsync(json, cancellationToken);

        await sutProvider.GetDependency<IHubContext<NotificationsHub>>().Clients.Received(1)
            .User(notification.UserId!.Value.ToString())
            .Received(1)
            .SendCoreAsync("ReceiveMessage", Arg.Is<object?[]>(objects =>
                    objects.Length == 1 && IsNotificationPushNotificationEqual(notification, objects[0],
                        PushType.Notification, contextId)),
                cancellationToken);
        sutProvider.GetDependency<IHubContext<NotificationsHub>>().Clients.Received(0).Group(Arg.Any<string>());
        sutProvider.GetDependency<IHubContext<AnonymousNotificationsHub>>().Clients.Received(0).User(Arg.Any<string>());
        sutProvider.GetDependency<IHubContext<AnonymousNotificationsHub>>().Clients.Received(0)
            .Group(Arg.Any<string>());
    }

    [Theory]
    [BitAutoData(false, ClientType.Browser)]
    [BitAutoData(false, ClientType.Desktop)]
    [BitAutoData(false, ClientType.Mobile)]
    [BitAutoData(false, ClientType.Web)]
    [BitAutoData(true, ClientType.Browser)]
    [BitAutoData(true, ClientType.Desktop)]
    [BitAutoData(true, ClientType.Mobile)]
    [BitAutoData(true, ClientType.Web)]
    public async Task
        SendNotificationToHubAsync_NotificationPushNotificationUserIdProvidedClientTypeNotAll_SentToGroupUserClientType(
            bool organizationIdProvided, ClientType clientType, SutProvider<HubHelpers> sutProvider,
            NotificationPushNotification notification,
            string contextId, CancellationToken cancellationToken)
    {
        notification.InstallationId = null;
        notification.ClientType = clientType;
        if (!organizationIdProvided)
        {
            notification.OrganizationId = null;
        }

        var json = ToNotificationJson(notification, PushType.Notification, contextId);
        await sutProvider.Sut.SendNotificationToHubAsync(json, cancellationToken);

        sutProvider.GetDependency<IHubContext<NotificationsHub>>().Clients.Received(0).User(Arg.Any<string>());
        await sutProvider.GetDependency<IHubContext<NotificationsHub>>().Clients.Received(1)
            .Group($"UserClientType_{notification.UserId!.Value}_{clientType}")
            .Received(1)
            .SendCoreAsync("ReceiveMessage", Arg.Is<object?[]>(objects =>
                    objects.Length == 1 && IsNotificationPushNotificationEqual(notification, objects[0],
                        PushType.Notification, contextId)),
                cancellationToken);
        sutProvider.GetDependency<IHubContext<AnonymousNotificationsHub>>().Clients.Received(0).User(Arg.Any<string>());
        sutProvider.GetDependency<IHubContext<AnonymousNotificationsHub>>().Clients.Received(0)
            .Group(Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    public async Task
        SendNotificationToHubAsync_NotificationPushNotificationOrganizationIdProvidedClientTypeAll_SentToGroupOrganization(
            SutProvider<HubHelpers> sutProvider, string contextId,
            NotificationPushNotification notification,
            CancellationToken cancellationToken)
    {
        notification.UserId = null;
        notification.InstallationId = null;
        notification.ClientType = ClientType.All;

        var json = ToNotificationJson(notification, PushType.Notification, contextId);
        await sutProvider.Sut.SendNotificationToHubAsync(json, cancellationToken);

        sutProvider.GetDependency<IHubContext<NotificationsHub>>().Clients.Received(0).User(Arg.Any<string>());
        await sutProvider.GetDependency<IHubContext<NotificationsHub>>().Clients.Received(1)
            .Group($"Organization_{notification.OrganizationId!.Value}")
            .Received(1)
            .SendCoreAsync("ReceiveMessage", Arg.Is<object?[]>(objects =>
                    objects.Length == 1 && IsNotificationPushNotificationEqual(notification, objects[0],
                        PushType.Notification, contextId)),
                cancellationToken);
        sutProvider.GetDependency<IHubContext<AnonymousNotificationsHub>>().Clients.Received(0).User(Arg.Any<string>());
        sutProvider.GetDependency<IHubContext<AnonymousNotificationsHub>>().Clients.Received(0)
            .Group(Arg.Any<string>());
    }

    [Theory]
    [BitAutoData(ClientType.Browser)]
    [BitAutoData(ClientType.Desktop)]
    [BitAutoData(ClientType.Mobile)]
    [BitAutoData(ClientType.Web)]
    public async Task
        SendNotificationToHubAsync_NotificationPushNotificationOrganizationIdProvidedClientTypeNotAll_SentToGroupOrganizationClientType(
            ClientType clientType, SutProvider<HubHelpers> sutProvider, string contextId,
            NotificationPushNotification notification,
            CancellationToken cancellationToken)
    {
        notification.UserId = null;
        notification.InstallationId = null;
        notification.ClientType = clientType;

        var json = ToNotificationJson(notification, PushType.Notification, contextId);
        await sutProvider.Sut.SendNotificationToHubAsync(json, cancellationToken);

        sutProvider.GetDependency<IHubContext<NotificationsHub>>().Clients.Received(0).User(Arg.Any<string>());
        await sutProvider.GetDependency<IHubContext<NotificationsHub>>().Clients.Received(1)
            .Group($"OrganizationClientType_{notification.OrganizationId!.Value}_{clientType}")
            .Received(1)
            .SendCoreAsync("ReceiveMessage", Arg.Is<object?[]>(objects =>
                    objects.Length == 1 && IsNotificationPushNotificationEqual(notification, objects[0],
                        PushType.Notification, contextId)),
                cancellationToken);
        sutProvider.GetDependency<IHubContext<AnonymousNotificationsHub>>().Clients.Received(0).User(Arg.Any<string>());
        sutProvider.GetDependency<IHubContext<AnonymousNotificationsHub>>().Clients.Received(0)
            .Group(Arg.Any<string>());
    }

    private static string ToNotificationJson(object payload, PushType type, string contextId)
    {
        var notification = new PushNotificationData<object>(type, payload, contextId);
        return JsonSerializer.Serialize(notification, JsonHelpers.IgnoreWritingNull);
    }

    private static bool IsNotificationPushNotificationEqual(NotificationPushNotification expected, object? actual,
        PushType type, string contextId)
    {
        if (actual is not PushNotificationData<NotificationPushNotification> pushNotificationData)
        {
            return false;
        }

        return pushNotificationData.Type == type &&
               pushNotificationData.ContextId == contextId &&
               expected.Id == pushNotificationData.Payload.Id &&
               expected.UserId == pushNotificationData.Payload.UserId &&
               expected.OrganizationId == pushNotificationData.Payload.OrganizationId &&
               expected.ClientType == pushNotificationData.Payload.ClientType &&
               expected.RevisionDate == pushNotificationData.Payload.RevisionDate;
    }
}
