using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Braintree;

namespace Bit.Core.Services
{
    public class BraintreePaymentService : IPaymentService
    {
        private readonly BraintreeGateway _gateway;

        public BraintreePaymentService(
            GlobalSettings globalSettings)
        {
            _gateway = new BraintreeGateway
            {
                Environment = globalSettings.Braintree.Production ?
                    Braintree.Environment.PRODUCTION : Braintree.Environment.SANDBOX,
                MerchantId = globalSettings.Braintree.MerchantId,
                PublicKey = globalSettings.Braintree.PublicKey,
                PrivateKey = globalSettings.Braintree.PrivateKey
            };
        }

        public async Task PurchasePremiumAsync(User user, string paymentToken, short additionalStorageGb)
        {
            var customerResult = await _gateway.Customer.CreateAsync(new CustomerRequest
            {
                PaymentMethodNonce = paymentToken,
                Email = user.Email
            });

            if(!customerResult.IsSuccess())
            {
                // error, throw something
            }

            var subId = "u" + user.Id.ToString("N").ToLower() +
                    Utilities.CoreHelpers.RandomString(3, upper: false, numeric: false);

            var subRequest = new SubscriptionRequest
            {
                Id = subId,
                PaymentMethodToken = paymentToken,
                PlanId = "premium-annually"
            };

            if(additionalStorageGb > 0)
            {
                subRequest.AddOns.Add = new AddAddOnRequest[]
                {
                    new AddAddOnRequest
                    {
                        InheritedFromId = "storage-gb-annually",
                        Quantity = additionalStorageGb
                    }
                };
            }

            var subResult = await _gateway.Subscription.CreateAsync(subRequest);

            if(!subResult.IsSuccess())
            {
                await _gateway.Customer.DeleteAsync(customerResult.Target.Id);
                // error, throw something
            }

            user.StripeCustomerId = customerResult.Target.Id;
            user.StripeSubscriptionId = subResult.Target.Id;
        }
    }
}
