using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Stripe;

namespace Bit.Core.Billing.Services.Implementations;

public class PremiumUserBillingService(
    IGlobalSettings globalSettings,
    IStripeAdapter stripeAdapter,
    ISubscriberService subscriberService,
    IUserRepository userRepository) : IPremiumUserBillingService
{
    public async Task Credit(User user, decimal amount)
    {
        var customer = await subscriberService.GetCustomer(user);

        // Negative credit represents a balance, and all Stripe denomination is in cents.
        var credit = (long)(amount * -100);

        if (customer == null)
        {
            var options = new CustomerCreateOptions
            {
                Balance = credit,
                Description = user.Name,
                Email = user.Email,
                InvoiceSettings = new CustomerInvoiceSettingsOptions
                {
                    CustomFields =
                    [
                        new CustomerInvoiceSettingsCustomFieldOptions
                        {
                            Name = user.SubscriberType(),
                            Value = user.SubscriberName().Length <= 30
                                ? user.SubscriberName()
                                : user.SubscriberName()[..30]
                        }
                    ]
                },
                Metadata = new Dictionary<string, string>
                {
                    ["region"] = globalSettings.BaseServiceUri.CloudRegion,
                    ["userId"] = user.Id.ToString()
                }
            };

            customer = await stripeAdapter.CreateCustomerAsync(options);

            user.Gateway = GatewayType.Stripe;
            user.GatewayCustomerId = customer.Id;
            await userRepository.ReplaceAsync(user);
        }
        else
        {
            var options = new CustomerUpdateOptions
            {
                Balance = customer.Balance + credit
            };

            await stripeAdapter.UpdateCustomerAsync(customer.Id, options);
        }
    }
}
