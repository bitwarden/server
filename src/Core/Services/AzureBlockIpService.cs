using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using System;

namespace Bit.Core.Services
{
    public class AzureBlockIpService : IBlockIpService
    {
        private readonly CloudQueue _blockIpQueue;
        private readonly CloudQueue _unblockIpQueue;

        public AzureBlockIpService(
            GlobalSettings globalSettings)
        {
            var storageAccount = CloudStorageAccount.Parse(globalSettings.Storage.ConnectionString);
            var queueClient = storageAccount.CreateCloudQueueClient();

            _blockIpQueue = queueClient.GetQueueReference("blockip");
            _blockIpQueue.CreateIfNotExists();

            _unblockIpQueue = queueClient.GetQueueReference("unblockip");
            _unblockIpQueue.CreateIfNotExists();
        }

        public async Task BlockIpAsync(string ipAddress, bool permanentBlock)
        {
            var message = new CloudQueueMessage(ipAddress);
            await _blockIpQueue.AddMessageAsync(message);

            if(!permanentBlock)
            {
                await _unblockIpQueue.AddMessageAsync(message, null, new TimeSpan(12, 0, 0), null, null);
            }
        }
    }
}
