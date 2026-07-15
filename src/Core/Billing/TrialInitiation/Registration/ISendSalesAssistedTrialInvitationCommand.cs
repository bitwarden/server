using Bit.Core.Billing.Enums;

namespace Bit.Core.Billing.TrialInitiation.Registration;

public interface ISendSalesAssistedTrialInvitationCommand
{
    Task HandleAsync(
        string email,
        string? name,
        string senderEmail,
        ProductTierType productTier,
        IEnumerable<ProductType> products,
        int trialLength,
        bool paymentOptional);
}
