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

        public async Task<bool> UpdateCustomerEmailAddress(string gatewayCustomerId, string emailAddress)
        {
            try
            {
                var customer = await _stripeAdapter.CustomerGetAsync(gatewayCustomerId);

                await _stripeAdapter.CustomerUpdateAsync(customer.Id,
                    new Stripe.CustomerUpdateOptions { Email = emailAddress });
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
