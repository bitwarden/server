using Azure.Storage.Queues;
using Bit.Core.Settings;

namespace Bit.Core.Services;

public class AzureQueueBlockIpService : IBlockIpService
{
    private readonly QueueClient _blockIpQueueClient;
    private readonly QueueClient _unblockIpQueueClient;
    private Tuple<string, bool, DateTime> _lastBlock;

    public AzureQueueBlockIpService(
        GlobalSettings globalSettings)
    {
        _blockIpQueueClient = new QueueClient(globalSettings.Storage.ConnectionString, "blockip");
        _unblockIpQueueClient = new QueueClient(globalSettings.Storage.ConnectionString, "unblockip");
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
        await _blockIpQueueClient.SendMessageAsync(ipAddress);
        if (!permanentBlock)
        {
            await _unblockIpQueueClient.SendMessageAsync(ipAddress, new TimeSpan(0, 15, 0));
        }
    }
}
