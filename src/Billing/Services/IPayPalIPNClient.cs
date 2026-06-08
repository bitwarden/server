namespace Bit.Billing.Services;

public interface IPayPalIPNClient
{
    Task<PayPalIPNVerificationResult> VerifyIPN(string transactionId, string formData);
}
