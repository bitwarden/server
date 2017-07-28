using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Stripe;
using System.Collections.Generic;

namespace Bit.Core.Services
{
    public class StripePaymentService : IPaymentService
    {
        private const string PremiumPlanId = "premium-annually";
        private const string StoragePlanId = "storage-gb-annually";

        public StripePaymentService(
            GlobalSettings globalSettings)
        {

        }

        public async Task PurchasePremiumAsync(User user, string paymentToken, short additionalStorageGb)
        {
            var customerService = new StripeCustomerService();
            var customer = await customerService.CreateAsync(new StripeCustomerCreateOptions
            {
                Description = user.Name,
                Email = user.Email,
                SourceToken = paymentToken
            });

            var subCreateOptions = new StripeSubscriptionCreateOptions
            {
                Items = new List<StripeSubscriptionItemOption>(),
                Metadata = new Dictionary<string, string>
                {
                    ["userId"] = user.Id.ToString()
                }
            };

            subCreateOptions.Items.Add(new StripeSubscriptionItemOption
            {
                PlanId = PremiumPlanId,
                Quantity = 1
            });

            if(additionalStorageGb > 0)
            {
                subCreateOptions.Items.Add(new StripeSubscriptionItemOption
                {
                    PlanId = StoragePlanId,
                    Quantity = additionalStorageGb
                });
            }

            StripeSubscription subscription = null;
            try
            {
                var subscriptionService = new StripeSubscriptionService();
                subscription = await subscriptionService.CreateAsync(customer.Id, subCreateOptions);
            }
            catch(StripeException)
            {
                await customerService.DeleteAsync(customer.Id);
                throw;
            }

            user.StripeCustomerId = customer.Id;
            user.StripeSubscriptionId = subscription.Id;
        }
    }
}
