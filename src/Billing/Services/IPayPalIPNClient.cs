namespace Bit.Billing.Services;

public interface IPayPalIPNClient
{
    Task<bool> VerifyIPN(string transactionId, string formData);
}
