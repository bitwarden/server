#nullable enable
using Bit.Core.Billing.Enums;

namespace Bit.Core.Billing.TrialInitiation.Registration;

public interface ISendTrialInitiationEmailForRegistrationCommand
{
    public Task<string?> Handle(
        string email,
        string? name,
        bool receiveMarketingEmails,
        ProductTierType productTier,
        IEnumerable<ProductType> products
    );
}
