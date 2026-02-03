using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;

namespace Bit.Core.Billing.Notifications;

public interface IPushNotificationAdapter
{
    Task NotifyBankAccountVerifiedAsync(Organization organization);
    Task NotifyBankAccountVerifiedAsync(Provider provider);
    Task NotifyEnabledChangedAsync(Organization organization);
}
