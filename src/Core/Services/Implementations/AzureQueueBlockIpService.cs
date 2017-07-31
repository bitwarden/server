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
            var message = new CloudQueueMessage(ipAddress);
            await _blockIpQueue.AddMessageAsync(message);

            if(!permanentBlock)
            {
                await _unblockIpQueue.AddMessageAsync(message, null, new TimeSpan(12, 0, 0), null, null);
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
