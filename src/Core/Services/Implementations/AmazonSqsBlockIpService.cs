using Amazon;
using Amazon.SQS;
using Bit.Core.Settings;

namespace Bit.Core.Services;

public class AmazonSqsBlockIpService : IBlockIpService, IDisposable
{
    private readonly IAmazonSQS _client;
    private string _blockIpQueueUrl;
    private string _unblockIpQueueUrl;
    private bool _didInit = false;
    private Tuple<string, bool, DateTime> _lastBlock;

    public AmazonSqsBlockIpService(
        GlobalSettings globalSettings)
        : this(globalSettings, new AmazonSQSClient(
            globalSettings.Amazon.AccessKeyId,
            globalSettings.Amazon.AccessKeySecret,
            RegionEndpoint.GetBySystemName(globalSettings.Amazon.Region))
        )
    {
    }

    public AmazonSqsBlockIpService(
        GlobalSettings globalSettings,
        IAmazonSQS amazonSqs)
    {
        if (string.IsNullOrWhiteSpace(globalSettings.Amazon?.AccessKeyId))
        {
            throw new ArgumentNullException(nameof(globalSettings.Amazon.AccessKeyId));
        }
        if (string.IsNullOrWhiteSpace(globalSettings.Amazon?.AccessKeySecret))
        {
            throw new ArgumentNullException(nameof(globalSettings.Amazon.AccessKeySecret));
        }
        if (string.IsNullOrWhiteSpace(globalSettings.Amazon?.Region))
        {
            throw new ArgumentNullException(nameof(globalSettings.Amazon.Region));
        }

        _client = amazonSqs;
    }

    public void Dispose()
    {
        _client?.Dispose();
    }

    public async Task BlockIpAsync(string ipAddress, bool permanentBlock)
    {
        var now = DateTime.UtcNow;
        if (_lastBlock != null && _lastBlock.Item1 == ipAddress && _lastBlock.Item2 == permanentBlock &&
            (now - _lastBlock.Item3) < TimeSpan.FromMinutes(1))
        {
            // Already blocked this IP recently.
            return;
        }

        _lastBlock = new Tuple<string, bool, DateTime>(ipAddress, permanentBlock, now);
        await _client.SendMessageAsync(_blockIpQueueUrl, ipAddress);
        if (!permanentBlock)
        {
            await _client.SendMessageAsync(_unblockIpQueueUrl, ipAddress);
        }
    }

    private async Task InitAsync()
    {
        if (_didInit)
        {
            return;
        }

        var blockIpQueue = await _client.GetQueueUrlAsync("block-ip");
        _blockIpQueueUrl = blockIpQueue.QueueUrl;
        var unblockIpQueue = await _client.GetQueueUrlAsync("unblock-ip");
        _unblockIpQueueUrl = unblockIpQueue.QueueUrl;
        _didInit = true;
    }
}
