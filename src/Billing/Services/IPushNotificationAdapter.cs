using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Providers.Entities;

namespace Bit.Billing.Services;

public interface IPushNotificationAdapter
{
    Task NotifyBankAccountVerifiedAsync(Organization organization);
    Task NotifyBankAccountVerifiedAsync(Provider provider);
    Task NotifyEnabledChangedAsync(Organization organization);
    Task NotifyPremiumStatusChangedAsync(User user);
}
