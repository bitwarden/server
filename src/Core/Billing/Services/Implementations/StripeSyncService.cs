using Bit.Core.Exceptions;
using Bit.Core.Services;

namespace Bit.Core.Billing.Services.Implementations;

public class StripeSyncService : IStripeSyncService
{
    private readonly IStripeAdapter _stripeAdapter;

    public StripeSyncService(IStripeAdapter stripeAdapter)
    {
        _stripeAdapter = stripeAdapter;
    }

    public async Task UpdateCustomerEmailAddress(string gatewayCustomerId, string emailAddress)
    {
        if (string.IsNullOrWhiteSpace(gatewayCustomerId))
        {
            throw new InvalidGatewayCustomerIdException();
        }

        if (string.IsNullOrWhiteSpace(emailAddress))
        {
            throw new InvalidEmailException();
        }

        var customer = await _stripeAdapter.GetCustomerAsync(gatewayCustomerId);

        await _stripeAdapter.UpdateCustomerAsync(customer.Id,
            new Stripe.CustomerUpdateOptions { Email = emailAddress });
    }
}
