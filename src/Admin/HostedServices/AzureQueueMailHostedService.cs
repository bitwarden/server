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
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Bit.Core.Utilities;

namespace Bit.Admin.HostedServices
{
    public class AzureQueueMailHostedService : IHostedService
    {
        private readonly JsonSerializer _jsonSerializer;
        private readonly ILogger<AzureQueueMailHostedService> _logger;
        private readonly GlobalSettings _globalSettings;
        private readonly IMailService _mailService;
        private CancellationTokenSource _cts;
        private Task _executingTask;

        private QueueClient _mailQueueClient;

        public AzureQueueMailHostedService(
            ILogger<AzureQueueMailHostedService> logger,
            IMailService mailService,
            GlobalSettings globalSettings)
        { 
            _logger = logger;
            _mailService = mailService;
            _globalSettings = globalSettings;

            _jsonSerializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                Converters = new[] { new EncodedStringConverter() },
            });
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
                        var token = JToken.Parse(message.MessageText);
                        if (token is JArray)
                        {
                            foreach (var mailQueueMessage in token.ToObject<List<MailQueueMessage>>(_jsonSerializer))
                            {
                                await _mailService.SendEnqueuedMailMessageAsync(mailQueueMessage);
                            }
                        }
                        else if (token is JObject)
                        {
                            var mailQueueMessage = token.ToObject<MailQueueMessage>();
                            await _mailService.SendEnqueuedMailMessageAsync(mailQueueMessage);
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
