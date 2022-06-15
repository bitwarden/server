using System.Threading.Tasks;

namespace Bit.Core.Services
{
    public class StripeSyncService : IStripeSyncService
    {
        private readonly IStripeAdapter _stripeAdapter;

        public StripeSyncService(IStripeAdapter stripeAdapter)
        {
            _stripeAdapter = stripeAdapter;
        }

        public async Task UpdateCustomerEmailAddress(string gatewayCustomerId, string emailAddress)
        {
            var customer = await _stripeAdapter.CustomerGetAsync(gatewayCustomerId);

            await _stripeAdapter.CustomerUpdateAsync(customer.Id,
                new Stripe.CustomerUpdateOptions { Email = emailAddress });
        }
    }
}
