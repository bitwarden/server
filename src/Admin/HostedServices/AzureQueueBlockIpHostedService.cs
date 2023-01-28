using Azure.Storage.Queues;
using Bit.Core.Settings;
using Microsoft.Extensions.Options;

namespace Bit.Admin.HostedServices;

public class AzureQueueBlockIpHostedService : BlockIpHostedService
{
    private QueueClient _blockIpQueueClient;
    private QueueClient _unblockIpQueueClient;

    public AzureQueueBlockIpHostedService(
        ILogger<AzureQueueBlockIpHostedService> logger,
        IOptions<AdminSettings> adminSettings,
        GlobalSettings globalSettings)
        : base(logger, adminSettings, globalSettings)
    { }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _blockIpQueueClient = new QueueClient(_globalSettings.Storage.ConnectionString, "blockip");
        _unblockIpQueueClient = new QueueClient(_globalSettings.Storage.ConnectionString, "unblockip");

        while (!cancellationToken.IsCancellationRequested)
        {
            var blockMessages = await _blockIpQueueClient.ReceiveMessagesAsync(maxMessages: 32);
            if (blockMessages.Value?.Any() ?? false)
            {
                foreach (var message in blockMessages.Value)
                {
                    try
                    {
                        await BlockIpAsync(message.MessageText, cancellationToken);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to block IP.");
                    }
                    await _blockIpQueueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);
                }
            }

            var unblockMessages = await _unblockIpQueueClient.ReceiveMessagesAsync(maxMessages: 32);
            if (unblockMessages.Value?.Any() ?? false)
            {
                foreach (var message in unblockMessages.Value)
                {
                    try
                    {
                        await UnblockIpAsync(message.MessageText, cancellationToken);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to unblock IP.");
                    }
                    await _unblockIpQueueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(15));
        }
    }
}
