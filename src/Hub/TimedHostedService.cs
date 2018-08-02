using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bit.Core;
using Bit.Core.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace Bit.Hub
{
    public class TimedHostedService : IHostedService, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IHubContext<SyncHub> _hubContext;
        private readonly GlobalSettings _globalSettings;

        private Task _executingTask;
        private CancellationTokenSource _cts;
        private CloudQueue _queue;

        public TimedHostedService(ILogger<TimedHostedService> logger, IHubContext<SyncHub> hubContext,
            GlobalSettings globalSettings)
        {
            _logger = logger;
            _hubContext = hubContext;
            _globalSettings = globalSettings;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _executingTask = ExecuteAsync(_cts.Token);
            return _executingTask.IsCompleted ? _executingTask : Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if(_executingTask == null)
            {
                return;
            }
            _cts.Cancel();
            await Task.WhenAny(_executingTask, Task.Delay(-1, cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
        }

        public void Dispose()
        {
            // TODO
        }

        private async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var storageAccount = CloudStorageAccount.Parse(_globalSettings.Events.ConnectionString);
            var queueClient = storageAccount.CreateCloudQueueClient();
            _queue = queueClient.GetQueueReference("sync");

            while(!cancellationToken.IsCancellationRequested)
            {
                var messages = await _queue.GetMessagesAsync(32, TimeSpan.FromMinutes(1),
                    null, null, cancellationToken);
                if(messages.Any())
                {
                    foreach(var message in messages)
                    {
                        var notification = JsonConvert.DeserializeObject<PushNotificationData<object>>(
                            message.AsString);
                        switch(notification.Type)
                        {
                            case Core.Enums.PushType.SyncCipherUpdate:
                            case Core.Enums.PushType.SyncCipherCreate:
                            case Core.Enums.PushType.SyncCipherDelete:
                            case Core.Enums.PushType.SyncLoginDelete:
                                var cipherNotification =
                                    JsonConvert.DeserializeObject<PushNotificationData<SyncCipherPushNotification>>(
                                        message.AsString);
                                if(cipherNotification.Payload.UserId.HasValue)
                                {
                                    await _hubContext.Clients.User(cipherNotification.Payload.UserId.ToString())
                                        .SendAsync("ReceiveMessage", notification, cancellationToken);
                                }
                                else if(cipherNotification.Payload.OrganizationId.HasValue)
                                {
                                    await _hubContext.Clients.Group(
                                        $"Organization_{cipherNotification.Payload.OrganizationId}")
                                        .SendAsync("ReceiveMessage", notification, cancellationToken);
                                }
                                break;
                            case Core.Enums.PushType.SyncFolderUpdate:
                            case Core.Enums.PushType.SyncFolderCreate:
                            case Core.Enums.PushType.SyncFolderDelete:
                                var folderNotification =
                                    JsonConvert.DeserializeObject<PushNotificationData<SyncFolderPushNotification>>(
                                        message.AsString);
                                await _hubContext.Clients.User(folderNotification.Payload.UserId.ToString())
                                        .SendAsync("ReceiveMessage", notification, cancellationToken);
                                break;
                            case Core.Enums.PushType.SyncCiphers:
                            case Core.Enums.PushType.SyncVault:
                            case Core.Enums.PushType.SyncOrgKeys:
                            case Core.Enums.PushType.SyncSettings:
                                var userNotification =
                                    JsonConvert.DeserializeObject<PushNotificationData<SyncUserPushNotification>>(
                                        message.AsString);
                                await _hubContext.Clients.User(userNotification.Payload.UserId.ToString())
                                        .SendAsync("ReceiveMessage", notification, cancellationToken);
                                break;
                            default:
                                break;
                        }
                        await _queue.DeleteMessageAsync(message);
                    }
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
        }
    }
}
