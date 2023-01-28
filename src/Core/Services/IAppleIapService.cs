using Bit.Billing.Models;

namespace Bit.Core.Services;

public interface IAppleIapService
{
    Task<AppleReceiptStatus> GetVerifiedReceiptStatusAsync(string receiptData);
    Task SaveReceiptAsync(AppleReceiptStatus receiptStatus, Guid userId);
    Task<Tuple<string, Guid?>> GetReceiptAsync(string originalTransactionId);
}
