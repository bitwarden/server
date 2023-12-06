using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.Billing.Commands;

public interface IRemovePaymentMethodCommand
{
    Task RemovePaymentMethod(Organization organization);
}
