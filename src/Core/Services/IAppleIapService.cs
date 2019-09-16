using System.Threading.Tasks;

namespace Bit.Core.Services
{
    public interface IAppleIapService
    {
        Task<bool> VerifyReceiptAsync(string receiptData);
    }
}
