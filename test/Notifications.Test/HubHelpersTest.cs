#nullable enable
using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Utilities;
using Bit.Notifications;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace Notifications.Test;

[SutProviderCustomize]
public class HubHelpersTest
{
    [Theory]
    [BitAutoData]
    public async Task SendNotificationToHubAsync_SyncNotificationGlobal_NothingSent(SutProvider<HubHelpers> sutProvider,
        ClientType clientType, string contextId, CancellationToken cancellationToke)
    {
        var message = new SyncNotificationPushNotification
        {
            Id = Guid.NewGuid(),
            UserId = null,
            OrganizationId = null,
            ClientType = clientType,
            RevisionDate = DateTime.UtcNow
        };

        var json = ToNotificationJson(message, PushType.SyncNotification, contextId);
        await sutProvider.Sut.SendNotificationToHubAsync(json, cancellationToke);

        sutProvider.GetDependency<IHubContext<NotificationsHub>>().Clients.Received(0).User(Arg.Any<string>());
        sutProvider.GetDependency<IHubContext<NotificationsHub>>().Clients.Received(0).Group(Arg.Any<string>());
        sutProvider.GetDependency<IHubContext<AnonymousNotificationsHub>>().Clients.Received(0).User(Arg.Any<string>());
        sutProvider.GetDependency<IHubContext<AnonymousNotificationsHub>>().Clients.Received(0)
            .Group(Arg.Any<string>());
    }

    [Theory]
    [BitAutoData(false)]
    [BitAutoData(true)]
    public async Task SendNotificationToHubAsync_SyncNotificationUserIdProvidedClientTypeAll_SentToUser(
        bool organizationIdProvided, SutProvider<HubHelpers> sutProvider, Guid userId, Guid organizationId,
        string contextId, CancellationToken cancellationToken)
    {
        var syncNotification = new SyncNotificationPushNotification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = organizationIdProvided ? organizationId : null,
            ClientType = ClientType.All,
            RevisionDate = DateTime.UtcNow
        };

        var json = ToNotificationJson(syncNotification, PushType.SyncNotification, contextId);
        await sutProvider.Sut.SendNotificationToHubAsync(json, cancellationToken);

        await sutProvider.GetDependency<IHubContext<NotificationsHub>>().Clients.Received(1)
            .User(userId.ToString())
            .Received(1)
            .SendCoreAsync("ReceiveMessage", Arg.Is<object?[]>(objects =>
                    objects.Length == 1 && IsSyncNotificationEqual(syncNotification, objects[0],
                        PushType.SyncNotification, contextId)),
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
        SendNotificationToHubAsync_SyncNotificationUserIdProvidedClientTypeNotAll_SentToGroupUserClientType(
            bool organizationIdProvided, ClientType clientType, SutProvider<HubHelpers> sutProvider, Guid userId,
            string contextId, CancellationToken cancellationToken)
    {
        var syncNotification = new SyncNotificationPushNotification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = organizationIdProvided ? Guid.NewGuid() : null,
            ClientType = clientType,
            RevisionDate = DateTime.UtcNow
        };

        var json = ToNotificationJson(syncNotification, PushType.SyncNotification, contextId);
        await sutProvider.Sut.SendNotificationToHubAsync(json, cancellationToken);

        sutProvider.GetDependency<IHubContext<NotificationsHub>>().Clients.Received(0).User(Arg.Any<string>());
        await sutProvider.GetDependency<IHubContext<NotificationsHub>>().Clients.Received(1)
            .Group($"UserClientType_{userId}_{clientType}")
            .Received(1)
            .SendCoreAsync("ReceiveMessage", Arg.Is<object?[]>(objects =>
                    objects.Length == 1 && IsSyncNotificationEqual(syncNotification, objects[0],
                        PushType.SyncNotification, contextId)),
                cancellationToken);
        sutProvider.GetDependency<IHubContext<AnonymousNotificationsHub>>().Clients.Received(0).User(Arg.Any<string>());
        sutProvider.GetDependency<IHubContext<AnonymousNotificationsHub>>().Clients.Received(0)
            .Group(Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    public async Task
        SendNotificationToHubAsync_SyncNotificationUserIdNullOrganizationIdProvidedClientTypeAll_SentToGroupOrganization(
            SutProvider<HubHelpers> sutProvider, string contextId, Guid organizationId,
            CancellationToken cancellationToken)
    {
        var syncNotification = new SyncNotificationPushNotification
        {
            Id = Guid.NewGuid(),
            UserId = null,
            OrganizationId = organizationId,
            ClientType = ClientType.All,
            RevisionDate = DateTime.UtcNow
        };

        var json = ToNotificationJson(syncNotification, PushType.SyncNotification, contextId);
        await sutProvider.Sut.SendNotificationToHubAsync(json, cancellationToken);

        sutProvider.GetDependency<IHubContext<NotificationsHub>>().Clients.Received(0).User(Arg.Any<string>());
        await sutProvider.GetDependency<IHubContext<NotificationsHub>>().Clients.Received(1)
            .Group($"Organization_{organizationId}")
            .Received(1)
            .SendCoreAsync("ReceiveMessage", Arg.Is<object?[]>(objects =>
                    objects.Length == 1 && IsSyncNotificationEqual(syncNotification, objects[0],
                        PushType.SyncNotification, contextId)),
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
        SendNotificationToHubAsync_SyncNotificationUserIdNullOrganizationIdProvidedClientTypeNotAll_SentToGroupOrganizationClientType(
            ClientType clientType, SutProvider<HubHelpers> sutProvider, string contextId, Guid organizationId,
            CancellationToken cancellationToken)
    {
        var syncNotification = new SyncNotificationPushNotification
        {
            Id = Guid.NewGuid(),
            UserId = null,
            OrganizationId = organizationId,
            ClientType = clientType,
            RevisionDate = DateTime.UtcNow
        };

        var json = ToNotificationJson(syncNotification, PushType.SyncNotification, contextId);
        await sutProvider.Sut.SendNotificationToHubAsync(json, cancellationToken);

        sutProvider.GetDependency<IHubContext<NotificationsHub>>().Clients.Received(0).User(Arg.Any<string>());
        await sutProvider.GetDependency<IHubContext<NotificationsHub>>().Clients.Received(1)
            .Group($"OrganizationClientType_{organizationId}_{clientType}")
            .Received(1)
            .SendCoreAsync("ReceiveMessage", Arg.Is<object?[]>(objects =>
                    objects.Length == 1 && IsSyncNotificationEqual(syncNotification, objects[0],
                        PushType.SyncNotification, contextId)),
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

    private static bool IsSyncNotificationEqual(SyncNotificationPushNotification expected, object? actual,
        PushType type, string contextId)
    {
        if (actual is not PushNotificationData<SyncNotificationPushNotification> pushNotificationData)
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
