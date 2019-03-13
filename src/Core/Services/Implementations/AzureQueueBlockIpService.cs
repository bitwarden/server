using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using System;
using Bit.Core.Utilities;

namespace Bit.Core.Services
{
    public class AzureQueueBlockIpService : IBlockIpService
    {
        private readonly CloudQueue _blockIpQueue;
        private readonly CloudQueue _unblockIpQueue;
        private bool _didInit = false;
        private Tuple<string, bool, DateTime> _lastBlock;

        public AzureQueueBlockIpService(
            GlobalSettings globalSettings)
        {
            var storageAccount = CloudStorageAccount.Parse(globalSettings.Storage.ConnectionString);
            var queueClient = storageAccount.CreateCloudQueueClient();

            _blockIpQueue = queueClient.GetQueueReference("blockip");
            _unblockIpQueue = queueClient.GetQueueReference("unblockip");
        }

        public async Task BlockIpAsync(string ipAddress, bool permanentBlock)
        {
            await InitAsync();
            var now = DateTime.UtcNow;
            if(_lastBlock != null && _lastBlock.Item1 == ipAddress && _lastBlock.Item2 == permanentBlock &&
                (now - _lastBlock.Item3) < TimeSpan.FromMinutes(1))
            {
                // Already blocked this IP recently.
                return;
            }

            _lastBlock = new Tuple<string, bool, DateTime>(ipAddress, permanentBlock, now);
            var message = new CloudQueueMessage(ipAddress);
            await _blockIpQueue.AddMessageAsync(message);
            if(!permanentBlock)
            {
                await _unblockIpQueue.AddMessageAsync(message, null, new TimeSpan(0, 15, 0), null, null);
            }
        }

        private async Task InitAsync()
        {
            if(_didInit)
            {
                return;
            }

            await _blockIpQueue.CreateIfNotExistsAsync();
            await _unblockIpQueue.CreateIfNotExistsAsync();
            _didInit = true;
        }
    }
}
