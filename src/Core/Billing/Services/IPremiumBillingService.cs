using Bit.Core.Billing.Models.Sales;

namespace Bit.Core.Billing.Services;

public interface IPremiumBillingService
{
    Task Finalize(PremiumSale sale);
}
