using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;

namespace Bit.Core.Services
{
    public class DeviceService : IDeviceService
    {
        private readonly IDeviceRepository _deviceRepository;

        public DeviceService(
            IDeviceRepository deviceRepository)
        {
            _deviceRepository = deviceRepository;
        }

        public async Task SaveAsync(Device device)
        {
            if(device.Id == default(Guid))
            {
                await _deviceRepository.CreateAsync(device);
            }
            else
            {
                device.RevisionDate = DateTime.UtcNow;
                await _deviceRepository.ReplaceAsync(device);
            }
        }
    }
}
