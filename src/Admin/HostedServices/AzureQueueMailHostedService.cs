using System.Text.Json;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Bit.Core.Models.Mail;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.Admin.HostedServices;

public class AzureQueueMailHostedService : IHostedService
{
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
                    using var document = JsonDocument.Parse(message.DecodeMessageText());
                    var root = document.RootElement;

                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var mailQueueMessage in root.ToObject<List<MailQueueMessage>>())
                        {
                            await _mailService.SendEnqueuedMailMessageAsync(mailQueueMessage);
                        }
                    }
                    else if (root.ValueKind == JsonValueKind.Object)
                    {
                        var mailQueueMessage = root.ToObject<MailQueueMessage>();
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
