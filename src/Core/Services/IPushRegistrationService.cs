using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public interface IPushRegistrationService
    {
        Task CreateOrUpdateRegistrationAsync(Device device);
        Task DeleteRegistrationAsync(Guid deviceId);
    }
}
