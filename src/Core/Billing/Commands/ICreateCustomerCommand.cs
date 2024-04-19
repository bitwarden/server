using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;

namespace Bit.Core.Billing.Commands;

public interface ICreateCustomerCommand
{
    /// <summary>
    /// Create a Stripe <see cref="Stripe.Customer"/> for the provided client <paramref name="organization"/> utilizing
    /// the address and tax information of its <paramref name="provider"/>.
    /// </summary>
    /// <param name="provider">The MSP that owns the client organization.</param>
    /// <param name="organization">The client organization to create a Stripe <see cref="Stripe.Customer"/> for.</param>
    Task CreateCustomer(
        Provider provider,
        Organization organization);
}
