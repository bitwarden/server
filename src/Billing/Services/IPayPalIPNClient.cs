namespace Bit.Billing.Services;

public interface IPayPalIPNClient
{
    Task<bool> VerifyIPN(Guid entityId, string formData);
}
