using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bit.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Bit.Admin.HostedServices
{
    public class AzureQueueBlockIpHostedService : BlockIpHostedService
    {
        private CloudQueue _blockQueue;
        private CloudQueue _unblockQueue;

        public AzureQueueBlockIpHostedService(
            ILogger<AzureQueueBlockIpHostedService> logger,
            IOptions<AdminSettings> adminSettings,
            GlobalSettings globalSettings)
            : base(logger, adminSettings, globalSettings)
        { }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var storageAccount = CloudStorageAccount.Parse(_globalSettings.Storage.ConnectionString);
            var queueClient = storageAccount.CreateCloudQueueClient();
            _blockQueue = queueClient.GetQueueReference("blockip");
            _unblockQueue = queueClient.GetQueueReference("unblockip");

            while(!cancellationToken.IsCancellationRequested)
            {
                var blockMessages = await _blockQueue.GetMessagesAsync(32, TimeSpan.FromSeconds(15),
                    null, null, cancellationToken);
                if(blockMessages.Any())
                {
                    foreach(var message in blockMessages)
                    {
                        try
                        {
                            await BlockIpAsync(message.AsString, cancellationToken);
                        }
                        catch(Exception e)
                        {
                            _logger.LogError(e, "Failed to block IP.");
                        }
                        await _blockQueue.DeleteMessageAsync(message);
                    }
                }

                var unblockMessages = await _unblockQueue.GetMessagesAsync(32, TimeSpan.FromSeconds(15),
                    null, null, cancellationToken);
                if(unblockMessages.Any())
                {
                    foreach(var message in unblockMessages)
                    {
                        try
                        {
                            await UnblockIpAsync(message.AsString, cancellationToken);
                        }
                        catch(Exception e)
                        {
                            _logger.LogError(e, "Failed to unblock IP.");
                        }
                        await _unblockQueue.DeleteMessageAsync(message);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(15));
            }
        }
    }
}
