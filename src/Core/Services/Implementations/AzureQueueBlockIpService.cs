using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using System;

namespace Bit.Core.Services
{
    public class AzureQueueBlockIpService : IBlockIpService
    {
        private readonly CloudQueue _blockIpQueue;
        private readonly CloudQueue _unblockIpQueue;
        private bool _didInit = false;

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
            var blockMessage = new CloudQueueMessage(ipAddress);
            await _blockIpQueue.AddMessageAsync(blockMessage);

            if(!permanentBlock)
            {
                var unblockMessage = new CloudQueueMessage(ipAddress);
                await _unblockIpQueue.AddMessageAsync(unblockMessage, null, new TimeSpan(0, 15, 0), null, null);
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
