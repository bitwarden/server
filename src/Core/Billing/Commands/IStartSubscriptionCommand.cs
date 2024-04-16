using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Models.Business;

namespace Bit.Core.Billing.Commands;

public interface IStartSubscriptionCommand
{
    Task StartSubscription(
        Provider provider,
        TaxInfo taxInfo);
}
