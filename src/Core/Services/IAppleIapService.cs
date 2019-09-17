using System;
using System.Threading.Tasks;

namespace Bit.Core.Services
{
    public interface IAppleIapService
    {
        Task<bool> VerifyReceiptAsync(string receiptData);
        Task<string> GetVerifiedLastTransactionIdAsync(string receiptData);
        Task<DateTime?> GetVerifiedLastExpiresDateAsync(string receiptData);
        string HashReceipt(string receiptData);
        Task SaveReceiptAsync(string receiptData);
        Task<string> GetReceiptAsync(string hash);
    }
}
