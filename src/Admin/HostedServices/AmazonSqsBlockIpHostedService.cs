using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using Bit.Core.Settings;
using Microsoft.Extensions.Options;

namespace Bit.Admin.HostedServices;

public class AmazonSqsBlockIpHostedService : BlockIpHostedService
{
    private AmazonSQSClient _client;

    public AmazonSqsBlockIpHostedService(
        ILogger<AmazonSqsBlockIpHostedService> logger,
        IOptions<AdminSettings> adminSettings,
        GlobalSettings globalSettings)
        : base(logger, adminSettings, globalSettings)
    { }

    public override void Dispose()
    {
        _client?.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _client = new AmazonSQSClient(_globalSettings.Amazon.AccessKeyId,
            _globalSettings.Amazon.AccessKeySecret, RegionEndpoint.GetBySystemName(_globalSettings.Amazon.Region));
        var blockIpQueue = await _client.GetQueueUrlAsync("block-ip", cancellationToken);
        var blockIpQueueUrl = blockIpQueue.QueueUrl;
        var unblockIpQueue = await _client.GetQueueUrlAsync("unblock-ip", cancellationToken);
        var unblockIpQueueUrl = unblockIpQueue.QueueUrl;

        while (!cancellationToken.IsCancellationRequested)
        {
            var blockMessageResponse = await _client.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = blockIpQueueUrl,
                MaxNumberOfMessages = 10,
                WaitTimeSeconds = 15
            }, cancellationToken);
            if (blockMessageResponse.Messages.Any())
            {
                foreach (var message in blockMessageResponse.Messages)
                {
                    try
                    {
                        await BlockIpAsync(message.Body, cancellationToken);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to block IP.");
                    }
                    await _client.DeleteMessageAsync(blockIpQueueUrl, message.ReceiptHandle, cancellationToken);
                }
            }

            var unblockMessageResponse = await _client.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = unblockIpQueueUrl,
                MaxNumberOfMessages = 10,
                WaitTimeSeconds = 15
            }, cancellationToken);
            if (unblockMessageResponse.Messages.Any())
            {
                foreach (var message in unblockMessageResponse.Messages)
                {
                    try
                    {
                        await UnblockIpAsync(message.Body, cancellationToken);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to unblock IP.");
                    }
                    await _client.DeleteMessageAsync(unblockIpQueueUrl, message.ReceiptHandle, cancellationToken);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(15));
        }
    }
}
