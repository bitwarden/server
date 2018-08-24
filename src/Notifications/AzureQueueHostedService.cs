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

namespace Bit.Notifications
{
    public class AzureQueueHostedService : IHostedService, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IHubContext<NotificationsHub> _hubContext;
        private readonly GlobalSettings _globalSettings;

        private Task _executingTask;
        private CancellationTokenSource _cts;
        private CloudQueue _queue;

        public AzureQueueHostedService(
            ILogger<AzureQueueHostedService> logger,
            IHubContext<NotificationsHub> hubContext,
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
        { }

        private async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var storageAccount = CloudStorageAccount.Parse(_globalSettings.Notifications.ConnectionString);
            var queueClient = storageAccount.CreateCloudQueueClient();
            _queue = queueClient.GetQueueReference("notifications");

            while(!cancellationToken.IsCancellationRequested)
            {
                var messages = await _queue.GetMessagesAsync(32, TimeSpan.FromMinutes(1),
                    null, null, cancellationToken);
                if(messages.Any())
                {
                    foreach(var message in messages)
                    {
                        var notificationJson = message.AsString;
                        var notification = JsonConvert.DeserializeObject<PushNotificationData<object>>(
                            notificationJson);
                        await HubHelpers.SendNotificationToHubAsync(notification.Type, notificationJson,
                            _hubContext, cancellationToken);
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
