using System.Threading.Tasks;

namespace Bit.Core.Services
{
    public interface IStripeSyncService
    {
        Task<bool> UpdateCustomerEmailAddress(string gatewayCustomerId, string emailAddress);
    }
}
