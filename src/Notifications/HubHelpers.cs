using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Models;
using Microsoft.AspNetCore.SignalR;

namespace Bit.Notifications;

public class HubHelpers
{
    private static readonly JsonSerializerOptions _deserializerOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly string _receiveMessageMethod = "ReceiveMessage";

    private readonly IHubContext<NotificationsHub> _hubContext;
    private readonly IHubContext<AnonymousNotificationsHub> _anonymousHubContext;
    private readonly ILogger<HubHelpers> _logger;

    public HubHelpers(IHubContext<NotificationsHub> hubContext,
        IHubContext<AnonymousNotificationsHub> anonymousHubContext,
        ILogger<HubHelpers> logger)
    {
        _hubContext = hubContext;
        _anonymousHubContext = anonymousHubContext;
        _logger = logger;
    }

    public async Task SendNotificationToHubAsync(string notificationJson, CancellationToken cancellationToken = default)
    {
        var notification =
            JsonSerializer.Deserialize<PushNotificationData<object>>(notificationJson, _deserializerOptions);
        if (notification is null)
        {
            return;
        }

        _logger.LogInformation("Sending notification: {NotificationType}", notification.Type);
        switch (notification.Type)
        {
            case PushType.SyncCipherUpdate:
            case PushType.SyncCipherCreate:
            case PushType.SyncCipherDelete:
            case PushType.SyncLoginDelete:
                var cipherNotification =
                    JsonSerializer.Deserialize<PushNotificationData<SyncCipherPushNotification>>(
                        notificationJson, _deserializerOptions);
                if (cipherNotification is null)
                {
                    break;
                }

                if (cipherNotification.Payload.UserId.HasValue)
                {
                    await _hubContext.Clients.User(cipherNotification.Payload.UserId.Value.ToString())
                        .SendAsync(_receiveMessageMethod, cipherNotification, cancellationToken);
                }
                else if (cipherNotification.Payload.OrganizationId.HasValue)
                {
                    await _hubContext.Clients
                        .Group(NotificationsHub.GetOrganizationGroup(cipherNotification.Payload.OrganizationId.Value))
                        .SendAsync(_receiveMessageMethod, cipherNotification, cancellationToken);
                }

                break;
            case PushType.SyncFolderUpdate:
            case PushType.SyncFolderCreate:
            case PushType.SyncFolderDelete:
                var folderNotification =
                    JsonSerializer.Deserialize<PushNotificationData<SyncFolderPushNotification>>(
                        notificationJson, _deserializerOptions);
                if (folderNotification is null)
                {
                    break;
                }

                await _hubContext.Clients.User(folderNotification.Payload.UserId.ToString())
                    .SendAsync(_receiveMessageMethod, folderNotification, cancellationToken);
                break;
            case PushType.SyncCiphers:
            case PushType.SyncVault:
            case PushType.SyncOrganizations:
            case PushType.SyncOrgKeys:
            case PushType.SyncSettings:
            case PushType.LogOut:
                var userNotification =
                    JsonSerializer.Deserialize<PushNotificationData<LogOutPushNotification>>(
                        notificationJson, _deserializerOptions);
                if (userNotification is null)
                {
                    break;
                }

                await _hubContext.Clients.User(userNotification.Payload.UserId.ToString())
                    .SendAsync(_receiveMessageMethod, userNotification, cancellationToken);
                break;
            case PushType.SyncSendCreate:
            case PushType.SyncSendUpdate:
            case PushType.SyncSendDelete:
                var sendNotification =
                    JsonSerializer.Deserialize<PushNotificationData<SyncSendPushNotification>>(
                        notificationJson, _deserializerOptions);
                if (sendNotification is null)
                {
                    break;
                }

                await _hubContext.Clients.User(sendNotification.Payload.UserId.ToString())
                    .SendAsync(_receiveMessageMethod, sendNotification, cancellationToken);
                break;
            case PushType.AuthRequestResponse:
                var authRequestResponseNotification =
                    JsonSerializer.Deserialize<PushNotificationData<AuthRequestPushNotification>>(
                        notificationJson, _deserializerOptions);
                if (authRequestResponseNotification is null)
                {
                    break;
                }

                await _anonymousHubContext.Clients.Group(authRequestResponseNotification.Payload.Id.ToString())
                    .SendAsync("AuthRequestResponseRecieved", authRequestResponseNotification, cancellationToken);
                break;
            case PushType.AuthRequest:
                var authRequestNotification =
                    JsonSerializer.Deserialize<PushNotificationData<AuthRequestPushNotification>>(
                        notificationJson, _deserializerOptions);
                if (authRequestNotification is null)
                {
                    break;
                }

                await _hubContext.Clients.User(authRequestNotification.Payload.UserId.ToString())
                    .SendAsync(_receiveMessageMethod, authRequestNotification, cancellationToken);
                break;
            case PushType.SyncOrganizationStatusChanged:
                var orgStatusNotification =
                    JsonSerializer.Deserialize<PushNotificationData<OrganizationStatusPushNotification>>(
                        notificationJson, _deserializerOptions);
                if (orgStatusNotification is null)
                {
                    break;
                }

                await _hubContext.Clients
                    .Group(NotificationsHub.GetOrganizationGroup(orgStatusNotification.Payload.OrganizationId))
                    .SendAsync(_receiveMessageMethod, orgStatusNotification, cancellationToken);
                break;
            case PushType.SyncOrganizationCollectionSettingChanged:
                var organizationCollectionSettingsChangedNotification =
                    JsonSerializer.Deserialize<PushNotificationData<OrganizationStatusPushNotification>>(
                        notificationJson, _deserializerOptions);
                if (organizationCollectionSettingsChangedNotification is null)
                {
                    break;
                }

                await _hubContext.Clients
                    .Group(NotificationsHub.GetOrganizationGroup(organizationCollectionSettingsChangedNotification
                        .Payload.OrganizationId))
                    .SendAsync(_receiveMessageMethod, organizationCollectionSettingsChangedNotification,
                        cancellationToken);
                break;
            case PushType.OrganizationBankAccountVerified:
                var organizationBankAccountVerifiedNotification =
                    JsonSerializer.Deserialize<PushNotificationData<OrganizationBankAccountVerifiedPushNotification>>(
                        notificationJson, _deserializerOptions);
                if (organizationBankAccountVerifiedNotification is null)
                {
                    break;
                }

                await _hubContext.Clients.Group(NotificationsHub.GetOrganizationGroup(organizationBankAccountVerifiedNotification.Payload.OrganizationId))
                    .SendAsync(_receiveMessageMethod, organizationBankAccountVerifiedNotification, cancellationToken);
                break;
            case PushType.ProviderBankAccountVerified:
                var providerBankAccountVerifiedNotification =
                    JsonSerializer.Deserialize<PushNotificationData<ProviderBankAccountVerifiedPushNotification>>(
                        notificationJson, _deserializerOptions);
                if (providerBankAccountVerifiedNotification is null)
                {
                    break;
                }

                await _hubContext.Clients.User(providerBankAccountVerifiedNotification.Payload.AdminId.ToString())
                    .SendAsync(_receiveMessageMethod, providerBankAccountVerifiedNotification, cancellationToken);
                break;
            case PushType.Notification:
            case PushType.NotificationStatus:
                var notificationData = JsonSerializer.Deserialize<PushNotificationData<NotificationPushNotification>>(
                    notificationJson, _deserializerOptions);
                if (notificationData is null)
                {
                    break;
                }

                if (notificationData.Payload.InstallationId.HasValue)
                {
                    await _hubContext.Clients.Group(NotificationsHub.GetInstallationGroup(
                            notificationData.Payload.InstallationId.Value, notificationData.Payload.ClientType))
                        .SendAsync(_receiveMessageMethod, notificationData, cancellationToken);
                }
                else if (notificationData.Payload.UserId.HasValue)
                {
                    if (notificationData.Payload.ClientType == ClientType.All)
                    {
                        await _hubContext.Clients.User(notificationData.Payload.UserId.Value.ToString())
                            .SendAsync(_receiveMessageMethod, notificationData, cancellationToken);
                    }
                    else
                    {
                        await _hubContext.Clients.Group(NotificationsHub.GetUserGroup(
                                notificationData.Payload.UserId.Value, notificationData.Payload.ClientType))
                            .SendAsync(_receiveMessageMethod, notificationData, cancellationToken);
                    }
                }
                else if (notificationData.Payload.OrganizationId.HasValue)
                {
                    await _hubContext.Clients.Group(NotificationsHub.GetOrganizationGroup(
                            notificationData.Payload.OrganizationId.Value, notificationData.Payload.ClientType))
                        .SendAsync(_receiveMessageMethod, notificationData, cancellationToken);
                }

                break;
            case PushType.RefreshSecurityTasks:
                var pendingTasksData =
                    JsonSerializer.Deserialize<PushNotificationData<UserPushNotification>>(notificationJson,
                        _deserializerOptions);
                if (pendingTasksData is null)
                {
                    break;
                }

                await _hubContext.Clients.User(pendingTasksData.Payload.UserId.ToString())
                    .SendAsync(_receiveMessageMethod, pendingTasksData, cancellationToken);
                break;
            case PushType.PolicyChanged:
                var policyData = JsonSerializer.Deserialize<PushNotificationData<SyncPolicyPushNotification>>(notificationJson, _deserializerOptions);
                if (policyData is null)
                {
                    break;
                }

                await _hubContext.Clients
                    .Group(NotificationsHub.GetOrganizationGroup(policyData.Payload.OrganizationId))
                    .SendAsync(_receiveMessageMethod, policyData, cancellationToken);
                break;
            default:
                _logger.LogWarning("Notification type '{NotificationType}' has not been registered in HubHelpers and will not be pushed as as result", notification.Type);
                break;
        }
    }
}
