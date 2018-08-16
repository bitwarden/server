using System.Threading;
using System.Threading.Tasks;
using Bit.Core.Models;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;

namespace Bit.Hub
{
    public static class HubHelpers
    {
        public static async Task SendNotificationToHubAsync(PushNotificationData<object> notification,
            IHubContext<SyncHub> hubContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            switch(notification.Type)
            {
                case Core.Enums.PushType.SyncCipherUpdate:
                case Core.Enums.PushType.SyncCipherCreate:
                case Core.Enums.PushType.SyncCipherDelete:
                case Core.Enums.PushType.SyncLoginDelete:
                    var cipherPayload = JsonConvert.DeserializeObject<SyncCipherPushNotification>(
                        JsonConvert.SerializeObject(notification.Payload));
                    if(cipherPayload.UserId.HasValue)
                    {
                        await hubContext.Clients.User(cipherPayload.UserId.ToString())
                            .SendAsync("ReceiveMessage", notification, cancellationToken);
                    }
                    else if(cipherPayload.OrganizationId.HasValue)
                    {
                        await hubContext.Clients.Group(
                            $"Organization_{cipherPayload.OrganizationId}")
                            .SendAsync("ReceiveMessage", notification, cancellationToken);
                    }
                    break;
                case Core.Enums.PushType.SyncFolderUpdate:
                case Core.Enums.PushType.SyncFolderCreate:
                case Core.Enums.PushType.SyncFolderDelete:
                    var folderPayload = JsonConvert.DeserializeObject<SyncFolderPushNotification>(
                         JsonConvert.SerializeObject(notification.Payload));
                    await hubContext.Clients.User(folderPayload.UserId.ToString())
                            .SendAsync("ReceiveMessage", notification, cancellationToken);
                    break;
                case Core.Enums.PushType.SyncCiphers:
                case Core.Enums.PushType.SyncVault:
                case Core.Enums.PushType.SyncOrgKeys:
                case Core.Enums.PushType.SyncSettings:
                    var userPayload = JsonConvert.DeserializeObject<SyncUserPushNotification>(
                         JsonConvert.SerializeObject(notification.Payload));
                    await hubContext.Clients.User(userPayload.UserId.ToString())
                            .SendAsync("ReceiveMessage", notification, cancellationToken);
                    break;
                default:
                    break;
            }
        }
    }
}
