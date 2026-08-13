using Bit.Core.Entities;

namespace Bit.Core.Billing.Services;

public interface IPremiumUserBillingService
{
    Task Credit(User user, decimal amount);
}
