using System;
using Microsoft.Extensions.Hosting;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging;
using Bit.Core.Settings;
using System.Threading.Tasks;
using System.Threading;
using Bit.Core.Services;
using Newtonsoft.Json;
using Bit.Core.Models.Mail;
using Azure.Storage.Queues.Models;
using System.Linq;
using Bit.Core.Models.Data;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Storage;

namespace Bit.Admin.HostedServices
{
    public class AzureQueueMailHostedService : IHostedService
    {
        private readonly ILogger<AzureQueueMailHostedService> _logger;
        private readonly GlobalSettings _globalSettings;
        private readonly IMailService _mailService;
        private CancellationTokenSource _cts;
        private Task _executingTask;

        private QueueClient _mailQueueClient;
        private CloudBlobContainer _queueMessageContainer;
        private CloudBlobClient _blobClient;

        public AzureQueueMailHostedService(
            ILogger<AzureQueueMailHostedService> logger,
            IMailService mailService,
            GlobalSettings globalSettings)
        { 
            _logger = logger;
            _mailService = mailService;
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
            if (_executingTask == null)
            {
                return;
            }
            _cts.Cancel();
            await Task.WhenAny(_executingTask, Task.Delay(-1, cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
        }

        private async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _mailQueueClient = new QueueClient(_globalSettings.Mail.ConnectionString, "mail");
            var storageAccount = CloudStorageAccount.Parse(_globalSettings.Mail.ConnectionString);
            _blobClient = storageAccount.CreateCloudBlobClient();
            _queueMessageContainer = _blobClient.GetContainerReference(AzureQueueMailService.QueueMessageContainerName);
            await _queueMessageContainer.CreateIfNotExistsAsync(BlobContainerPublicAccessType.Blob, null, null);

            QueueMessage[] mailMessages;
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!(mailMessages = await RetrieveMessagesAsync()).Any())
                {
                    await Task.Delay(TimeSpan.FromSeconds(15));
                }

                foreach (var message in mailMessages)
                {
                    try
                    {
                        var queueMessage = JsonConvert.DeserializeObject<AzureQueueMessage<MailQueueMessage>>(message.MessageText);
                        if (queueMessage.BlobBackedMessage)
                        {
                            var blob = _queueMessageContainer.GetBlockBlobReference($"{queueMessage.MessageId}");
                            var fullMessageJson = await blob.DownloadTextAsync();
                            queueMessage = JsonConvert.DeserializeObject<AzureQueueMessage<MailQueueMessage>>(fullMessageJson);
                            await blob.DeleteAsync();
                        }

                        if (queueMessage.Message != null)
                        {
                            await _mailService.SendEnqueuedMailMessageAsync(queueMessage.Message);
                        }

                        if (queueMessage.Messages != null)
                        {
                            foreach (var mailQueueMessage in queueMessage.Messages)
                            {
                                await _mailService.SendEnqueuedMailMessageAsync(mailQueueMessage);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to send email");
                        // TODO: retries?
                    }
                    
                    await _mailQueueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
        }

        private async Task<QueueMessage[]> RetrieveMessagesAsync()
        {
            return (await _mailQueueClient.ReceiveMessagesAsync(maxMessages: 32))?.Value ?? new QueueMessage[] { };
        }
    }
}
