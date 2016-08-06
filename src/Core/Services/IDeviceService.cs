using System.Threading.Tasks;
using Bit.Core.Domains;

namespace Bit.Core.Services
{
    public interface IDeviceService
    {
        Task SaveAsync(Device device);
    }
}
