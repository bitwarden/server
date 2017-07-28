using System;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Braintree;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;

namespace Bit.Core.Services
{
    public class BraintreePaymentService : IPaymentService
    {
        private const string PremiumPlanId = "premium-annually";
        private const string StoragePlanId = "storage-gb-annually";
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

        public async Task AdjustStorageAsync(IStorableSubscriber storableSubscriber, int additionalStorage, string storagePlanId)
        {
            var sub = await _gateway.Subscription.FindAsync(storableSubscriber.StripeSubscriptionId);
            if(sub == null)
            {
                throw new GatewayException("Subscription was not found.");
            }

            var req = new SubscriptionRequest
            {
                AddOns = new AddOnsRequest(),
                Options = new SubscriptionOptionsRequest
                {
                    ProrateCharges = true
                }
            };

            var storageItem = sub.AddOns?.FirstOrDefault(a => a.Id == storagePlanId);
            if(additionalStorage > 0 && storageItem == null)
            {
                req.AddOns.Add = new AddAddOnRequest[]
                {
                    new AddAddOnRequest
                    {
                        InheritedFromId = storagePlanId,
                        Quantity = additionalStorage,
                        NeverExpires = true
                    }
                };
            }
            else if(additionalStorage > 0 && storageItem != null)
            {
                req.AddOns.Update = new UpdateAddOnRequest[]
                {
                    new UpdateAddOnRequest
                    {
                        ExistingId = storageItem.Id,
                        Quantity = additionalStorage
                    }
                };
            }
            else if(additionalStorage == 0 && storageItem != null)
            {
                req.AddOns.Remove = new string[] { storageItem.Id };
            }

            var result = await _gateway.Subscription.UpdateAsync(sub.Id, req);
            if(!result.IsSuccess())
            {
                throw new GatewayException("Failed to adjust storage.");
            }
        }

        public async Task CancelAndRecoverChargesAsync(ISubscriber subscriber)
        {
            if(!string.IsNullOrWhiteSpace(subscriber.StripeSubscriptionId))
            {
                await _gateway.Subscription.CancelAsync(subscriber.StripeSubscriptionId);
            }

            if(string.IsNullOrWhiteSpace(subscriber.StripeCustomerId))
            {
                return;
            }

            var transactionRequest = new TransactionSearchRequest().CustomerId.Is(subscriber.StripeCustomerId);
            var transactions = _gateway.Transaction.Search(transactionRequest);

            if((transactions?.MaximumCount ?? 0) > 0)
            {
                foreach(var transaction in transactions.Cast<Transaction>().Where(c => c.RefundedTransactionId == null))
                {
                    await _gateway.Transaction.RefundAsync(transaction.Id);
                }
            }

            await _gateway.Customer.DeleteAsync(subscriber.StripeCustomerId);
        }

        public async Task CancelSubscriptionAsync(ISubscriber subscriber, bool endOfPeriod = false)
        {
            if(subscriber == null)
            {
                throw new ArgumentNullException(nameof(subscriber));
            }

            if(string.IsNullOrWhiteSpace(subscriber.StripeSubscriptionId))
            {
                throw new GatewayException("No subscription.");
            }

            var sub = await _gateway.Subscription.FindAsync(subscriber.StripeSubscriptionId);
            if(sub == null)
            {
                throw new GatewayException("Subscription was not found.");
            }

            if(sub.Status == SubscriptionStatus.CANCELED || sub.Status == SubscriptionStatus.EXPIRED ||
                !sub.NeverExpires.GetValueOrDefault())
            {
                throw new GatewayException("Subscription is already canceled.");
            }

            if(endOfPeriod)
            {
                var req = new SubscriptionRequest
                {
                    NeverExpires = false,
                    NumberOfBillingCycles = sub.CurrentBillingCycle
                };

                var result = await _gateway.Subscription.UpdateAsync(subscriber.StripeSubscriptionId, req);
                if(!result.IsSuccess())
                {
                    throw new GatewayException("Unable to cancel subscription.");
                }
            }
            else
            {
                var result = await _gateway.Subscription.CancelAsync(subscriber.StripeSubscriptionId);
                if(!result.IsSuccess())
                {
                    throw new GatewayException("Unable to cancel subscription.");
                }
            }
        }

        public async Task<BillingInfo> GetBillingAsync(ISubscriber subscriber)
        {
            var billingInfo = new BillingInfo();
            if(!string.IsNullOrWhiteSpace(subscriber.StripeCustomerId))
            {
                var customer = await _gateway.Customer.FindAsync(subscriber.StripeCustomerId);
                if(customer != null)
                {
                    if(customer.DefaultPaymentMethod != null)
                    {
                        billingInfo.PaymentSource = new BillingInfo.BillingSource(customer.DefaultPaymentMethod);
                    }

                    var transactionRequest = new TransactionSearchRequest().CustomerId.Is(customer.Id);
                    var transactions = _gateway.Transaction.Search(transactionRequest);
                    billingInfo.Charges = transactions?.Cast<Transaction>().OrderByDescending(t => t.CreatedAt)
                        .Select(t => new BillingInfo.BillingCharge(t));
                }
            }

            if(!string.IsNullOrWhiteSpace(subscriber.StripeSubscriptionId))
            {
                var sub = await _gateway.Subscription.FindAsync(subscriber.StripeSubscriptionId);
                if(sub != null)
                {
                    var plans = await _gateway.Plan.AllAsync();
                    var plan = plans?.FirstOrDefault(p => p.Id == sub.PlanId);
                    billingInfo.Subscription = new BillingInfo.BillingSubscription(sub, plan);
                }

                if(sub.NextBillingDate.HasValue)
                {
                    billingInfo.UpcomingInvoice = new BillingInfo.BillingInvoice(sub);
                }
            }

            return billingInfo;
        }

        public async Task PurchasePremiumAsync(User user, string paymentToken, short additionalStorageGb)
        {
            var customerResult = await _gateway.Customer.CreateAsync(new CustomerRequest
            {
                PaymentMethodNonce = paymentToken,
                Email = user.Email
            });

            if(!customerResult.IsSuccess() || customerResult.Target.PaymentMethods.Length == 0)
            {
                throw new GatewayException("Failed to create customer.");
            }

            var subId = "u" + user.Id.ToString("N").ToLower() +
                    Utilities.CoreHelpers.RandomString(3, upper: false, numeric: false);

            var subRequest = new SubscriptionRequest
            {
                Id = subId,
                PaymentMethodToken = customerResult.Target.PaymentMethods[0].Token,
                PlanId = PremiumPlanId
            };

            if(additionalStorageGb > 0)
            {
                subRequest.AddOns = new AddOnsRequest();
                subRequest.AddOns.Add = new AddAddOnRequest[]
                {
                    new AddAddOnRequest
                    {
                        InheritedFromId = StoragePlanId,
                        Quantity = additionalStorageGb
                    }
                };
            }

            var subResult = await _gateway.Subscription.CreateAsync(subRequest);

            if(!subResult.IsSuccess())
            {
                await _gateway.Customer.DeleteAsync(customerResult.Target.Id);
                throw new GatewayException("Failed to create subscription.");
            }

            user.StripeCustomerId = customerResult.Target.Id;
            user.StripeSubscriptionId = subResult.Target.Id;
        }

        public async Task ReinstateSubscriptionAsync(ISubscriber subscriber)
        {
            if(subscriber == null)
            {
                throw new ArgumentNullException(nameof(subscriber));
            }

            if(string.IsNullOrWhiteSpace(subscriber.StripeSubscriptionId))
            {
                throw new GatewayException("No subscription.");
            }

            var sub = await _gateway.Subscription.FindAsync(subscriber.StripeSubscriptionId);
            if(sub == null)
            {
                throw new GatewayException("Subscription was not found.");
            }

            if(sub.Status != SubscriptionStatus.ACTIVE || sub.NeverExpires.GetValueOrDefault())
            {
                throw new GatewayException("Subscription is not marked for cancellation.");
            }

            var req = new SubscriptionRequest
            {
                NeverExpires = true,
                NumberOfBillingCycles = null
            };

            var result = await _gateway.Subscription.UpdateAsync(subscriber.StripeSubscriptionId, req);
            if(!result.IsSuccess())
            {
                throw new GatewayException("Unable to reinstate subscription.");
            }
        }

        public async Task<bool> UpdatePaymentMethodAsync(ISubscriber subscriber, string paymentToken)
        {
            if(subscriber == null)
            {
                throw new ArgumentNullException(nameof(subscriber));
            }

            var updatedSubscriber = false;
            Customer customer = null;

            if(!string.IsNullOrWhiteSpace(subscriber.StripeCustomerId))
            {
                customer = await _gateway.Customer.FindAsync(subscriber.StripeCustomerId);
            }

            if(customer == null)
            {
                var result = await _gateway.Customer.CreateAsync(new CustomerRequest
                {
                    Email = subscriber.BillingEmailAddress(),
                    PaymentMethodNonce = paymentToken
                });

                if(!result.IsSuccess())
                {
                    throw new GatewayException("Cannot create customer.");
                }

                customer = result.Target;
                subscriber.StripeCustomerId = customer.Id;
                updatedSubscriber = true;
            }
            else
            {
                if(customer.DefaultPaymentMethod != null)
                {
                    var deleteResult = await _gateway.PaymentMethod.DeleteAsync(customer.DefaultPaymentMethod.Token);
                    if(!deleteResult.IsSuccess())
                    {
                        throw new GatewayException("Cannot delete old payment method.");
                    }
                }

                var result = await _gateway.PaymentMethod.CreateAsync(new PaymentMethodRequest
                {
                    PaymentMethodNonce = paymentToken,
                    CustomerId = customer.Id
                });
                if(!result.IsSuccess())
                {
                    throw new GatewayException("Cannot add new payment method.");
                }
            }

            return updatedSubscriber;
        }
    }
}
