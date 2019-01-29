using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Stripe;
using System.Collections.Generic;
using Bit.Core.Exceptions;
using System.Linq;
using Bit.Core.Models.Business;
using Braintree;
using Bit.Core.Enums;

namespace Bit.Core.Services
{
    public class StripePaymentService : IPaymentService
    {
        private const string PremiumPlanId = "premium-annually";
        private const string StoragePlanId = "storage-gb-annually";
        private readonly BraintreeGateway _btGateway;

        public StripePaymentService(
            GlobalSettings globalSettings)
        {
            _btGateway = new BraintreeGateway
            {
                Environment = globalSettings.Braintree.Production ?
                    Braintree.Environment.PRODUCTION : Braintree.Environment.SANDBOX,
                MerchantId = globalSettings.Braintree.MerchantId,
                PublicKey = globalSettings.Braintree.PublicKey,
                PrivateKey = globalSettings.Braintree.PrivateKey
            };
        }

        public async Task PurchasePremiumAsync(User user, PaymentMethodType paymentMethodType, string paymentToken,
            short additionalStorageGb)
        {
            Customer braintreeCustomer = null;
            StripeBilling? stripeSubscriptionBilling = null;
            string stipeCustomerSourceToken = null;
            var stripeCustomerMetadata = new Dictionary<string, string>();

            if(paymentMethodType == PaymentMethodType.PayPal)
            {
                stripeSubscriptionBilling = StripeBilling.SendInvoice;
                var randomSuffix = Utilities.CoreHelpers.RandomString(3, upper: false, numeric: false);
                var customerResult = await _btGateway.Customer.CreateAsync(new CustomerRequest
                {
                    PaymentMethodNonce = paymentToken,
                    Email = user.Email,
                    Id = "u" + user.Id.ToString("N").ToLower() + randomSuffix
                });

                if(!customerResult.IsSuccess() || customerResult.Target.PaymentMethods.Length == 0)
                {
                    throw new GatewayException("Failed to create PayPal customer record.");
                }

                braintreeCustomer = customerResult.Target;
                stripeCustomerMetadata.Add("btCustomerId", braintreeCustomer.Id);
            }
            else if(paymentMethodType == PaymentMethodType.Card || paymentMethodType == PaymentMethodType.BankAccount)
            {
                stipeCustomerSourceToken = paymentToken;
            }

            var customerService = new StripeCustomerService();
            var customer = await customerService.CreateAsync(new StripeCustomerCreateOptions
            {
                Description = user.Name,
                Email = user.Email,
                SourceToken = stipeCustomerSourceToken,
                Metadata = stripeCustomerMetadata
            });

            var subCreateOptions = new StripeSubscriptionCreateOptions
            {
                CustomerId = customer.Id,
                Items = new List<StripeSubscriptionItemOption>(),
                Billing = stripeSubscriptionBilling,
                DaysUntilDue = stripeSubscriptionBilling != null ? 1 : 0,
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
                subscription = await subscriptionService.CreateAsync(subCreateOptions);

                if(stripeSubscriptionBilling == StripeBilling.SendInvoice)
                {
                    var invoiceService = new StripeInvoiceService();
                    var invoices = await invoiceService.ListAsync(new StripeInvoiceListOptions
                    {
                        SubscriptionId = subscription.Id
                    });

                    var invoice = invoices?.FirstOrDefault(i => i.AmountDue > 0);
                    if(invoice == null)
                    {
                        throw new GatewayException("Invoice not found.");
                    }

                    if(braintreeCustomer != null)
                    {
                        var btInvoiceAmount = (invoice.AmountDue / 100M);
                        var transactionResult = await _btGateway.Transaction.SaleAsync(new TransactionRequest
                        {
                            Amount = btInvoiceAmount,
                            CustomerId = braintreeCustomer.Id
                        });

                        if(!transactionResult.IsSuccess() || transactionResult.Target.Amount != btInvoiceAmount)
                        {
                            throw new GatewayException("Failed to charge PayPal customer.");
                        }

                        var invoiceItemService = new StripeInvoiceItemService();
                        await invoiceItemService.CreateAsync(new StripeInvoiceItemCreateOptions
                        {
                            Currency = "USD",
                            CustomerId = customer.Id,
                            InvoiceId = invoice.Id,
                            Amount = -1 * invoice.AmountDue,
                            Description = $"PayPal Credit, Transaction ID " +
                                transactionResult.Target.PayPalDetails.AuthorizationId,
                            Metadata = new Dictionary<string, string>
                            {
                                ["btTransactionId"] = transactionResult.Target.Id,
                                ["btPayPalTransactionId"] = transactionResult.Target.PayPalDetails.AuthorizationId
                            }
                        });
                    }
                    else
                    {
                        throw new GatewayException("No payment was able to be collected.");
                    }

                    await invoiceService.PayAsync(invoice.Id, new StripeInvoicePayOptions { });
                }
            }
            catch(Exception e)
            {
                await customerService.DeleteAsync(customer.Id);
                if(braintreeCustomer != null)
                {
                    await _btGateway.Customer.DeleteAsync(braintreeCustomer.Id);
                }
                throw e;
            }

            user.Gateway = GatewayType.Stripe;
            user.GatewayCustomerId = customer.Id;
            user.GatewaySubscriptionId = subscription.Id;
            user.Premium = true;
            user.PremiumExpirationDate = subscription.CurrentPeriodEnd;
        }

        public async Task AdjustStorageAsync(IStorableSubscriber storableSubscriber, int additionalStorage,
            string storagePlanId)
        {
            var subscriptionItemService = new StripeSubscriptionItemService();
            var subscriptionService = new StripeSubscriptionService();
            var sub = await subscriptionService.GetAsync(storableSubscriber.GatewaySubscriptionId);
            if(sub == null)
            {
                throw new GatewayException("Subscription not found.");
            }

            var storageItem = sub.Items?.Data?.FirstOrDefault(i => i.Plan.Id == storagePlanId);
            if(additionalStorage > 0 && storageItem == null)
            {
                await subscriptionItemService.CreateAsync(new StripeSubscriptionItemCreateOptions
                {
                    PlanId = storagePlanId,
                    Quantity = additionalStorage,
                    Prorate = true,
                    SubscriptionId = sub.Id
                });
            }
            else if(additionalStorage > 0 && storageItem != null)
            {
                await subscriptionItemService.UpdateAsync(storageItem.Id, new StripeSubscriptionItemUpdateOptions
                {
                    PlanId = storagePlanId,
                    Quantity = additionalStorage,
                    Prorate = true
                });
            }
            else if(additionalStorage == 0 && storageItem != null)
            {
                await subscriptionItemService.DeleteAsync(storageItem.Id);
            }

            if(additionalStorage > 0)
            {
                await PreviewUpcomingInvoiceAndPayAsync(storableSubscriber, storagePlanId, 400);
            }
        }

        public async Task CancelAndRecoverChargesAsync(ISubscriber subscriber)
        {
            if(!string.IsNullOrWhiteSpace(subscriber.GatewaySubscriptionId))
            {
                var subscriptionService = new StripeSubscriptionService();
                await subscriptionService.CancelAsync(subscriber.GatewaySubscriptionId,
                    new StripeSubscriptionCancelOptions());
            }

            if(string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
            {
                return;
            }

            var chargeService = new StripeChargeService();
            var charges = await chargeService.ListAsync(new StripeChargeListOptions
            {
                CustomerId = subscriber.GatewayCustomerId
            });

            if(charges?.Data != null)
            {
                var refundService = new StripeRefundService();
                foreach(var charge in charges.Data.Where(c => !c.Refunded))
                {
                    await refundService.CreateAsync(charge.Id);
                }
            }

            var customerService = new StripeCustomerService();
            await customerService.DeleteAsync(subscriber.GatewayCustomerId);
        }

        public async Task PreviewUpcomingInvoiceAndPayAsync(ISubscriber subscriber, string planId,
            int prorateThreshold = 500)
        {
            var invoiceService = new StripeInvoiceService();
            var upcomingPreview = await invoiceService.UpcomingAsync(subscriber.GatewayCustomerId,
                new StripeUpcomingInvoiceOptions
                {
                    SubscriptionId = subscriber.GatewaySubscriptionId
                });

            var prorationAmount = upcomingPreview.StripeInvoiceLineItems?.Data?
                .TakeWhile(i => i.Plan.Id == planId && i.Proration).Sum(i => i.Amount);
            if(prorationAmount.GetValueOrDefault() >= prorateThreshold)
            {
                try
                {
                    // Owes more than prorateThreshold on next invoice.
                    // Invoice them and pay now instead of waiting until next billing cycle.
                    var invoice = await invoiceService.CreateAsync(subscriber.GatewayCustomerId,
                        new StripeInvoiceCreateOptions
                        {
                            SubscriptionId = subscriber.GatewaySubscriptionId
                        });

                    if(invoice.AmountDue > 0)
                    {
                        var customerService = new StripeCustomerService();
                        var customer = await customerService.GetAsync(subscriber.GatewayCustomerId);
                        if(customer != null)
                        {
                            if(customer.Metadata.ContainsKey("btCustomerId"))
                            {
                                var invoiceAmount = (invoice.AmountDue / 100M);
                                var transactionResult = await _btGateway.Transaction.SaleAsync(new TransactionRequest
                                {
                                    Amount = invoiceAmount,
                                    CustomerId = customer.Metadata["btCustomerId"]
                                });

                                if(!transactionResult.IsSuccess() || transactionResult.Target.Amount != invoiceAmount)
                                {
                                    await invoiceService.UpdateAsync(invoice.Id, new StripeInvoiceUpdateOptions
                                    {
                                        Closed = true
                                    });
                                    throw new GatewayException("Failed to charge PayPal customer.");
                                }

                                await customerService.UpdateAsync(customer.Id, new StripeCustomerUpdateOptions
                                {
                                    AccountBalance = customer.AccountBalance - invoice.AmountDue,
                                    Metadata = customer.Metadata
                                });
                            }
                        }

                        await invoiceService.PayAsync(invoice.Id, new StripeInvoicePayOptions());
                    }
                }
                catch(StripeException) { }
            }
        }

        public async Task CancelSubscriptionAsync(ISubscriber subscriber, bool endOfPeriod = false)
        {
            if(subscriber == null)
            {
                throw new ArgumentNullException(nameof(subscriber));
            }

            if(string.IsNullOrWhiteSpace(subscriber.GatewaySubscriptionId))
            {
                throw new GatewayException("No subscription.");
            }

            var subscriptionService = new StripeSubscriptionService();
            var sub = await subscriptionService.GetAsync(subscriber.GatewaySubscriptionId);
            if(sub == null)
            {
                throw new GatewayException("Subscription was not found.");
            }

            if(sub.CanceledAt.HasValue || sub.Status == "canceled" || sub.Status == "unpaid")
            {
                // Already canceled
                return;
            }

            try
            {
                var canceledSub = endOfPeriod ?
                    await subscriptionService.UpdateAsync(sub.Id,
                        new StripeSubscriptionUpdateOptions { CancelAtPeriodEnd = true }) :
                    await subscriptionService.CancelAsync(sub.Id, new StripeSubscriptionCancelOptions());
                if(!canceledSub.CanceledAt.HasValue)
                {
                    throw new GatewayException("Unable to cancel subscription.");
                }
            }
            catch(StripeException e)
            {
                if(e.Message != $"No such subscription: {subscriber.GatewaySubscriptionId}")
                {
                    throw e;
                }
            }
        }

        public async Task ReinstateSubscriptionAsync(ISubscriber subscriber)
        {
            if(subscriber == null)
            {
                throw new ArgumentNullException(nameof(subscriber));
            }

            if(string.IsNullOrWhiteSpace(subscriber.GatewaySubscriptionId))
            {
                throw new GatewayException("No subscription.");
            }

            var subscriptionService = new StripeSubscriptionService();
            var sub = await subscriptionService.GetAsync(subscriber.GatewaySubscriptionId);
            if(sub == null)
            {
                throw new GatewayException("Subscription was not found.");
            }

            if((sub.Status != "active" && sub.Status != "trialing") || !sub.CanceledAt.HasValue)
            {
                throw new GatewayException("Subscription is not marked for cancellation.");
            }

            var updatedSub = await subscriptionService.UpdateAsync(sub.Id,
                new StripeSubscriptionUpdateOptions { CancelAtPeriodEnd = false });
            if(updatedSub.CanceledAt.HasValue)
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

            if(subscriber.Gateway.HasValue && subscriber.Gateway.Value != Enums.GatewayType.Stripe)
            {
                throw new GatewayException("Switching from one payment type to another is not supported. " +
                    "Contact us for assistance.");
            }

            var updatedSubscriber = false;

            var cardService = new StripeCardService();
            var bankSerice = new BankAccountService();
            var customerService = new StripeCustomerService();
            StripeCustomer customer = null;

            if(!string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
            {
                customer = await customerService.GetAsync(subscriber.GatewayCustomerId);
            }

            if(customer == null)
            {
                customer = await customerService.CreateAsync(new StripeCustomerCreateOptions
                {
                    Description = subscriber.BillingName(),
                    Email = subscriber.BillingEmailAddress(),
                    SourceToken = paymentToken
                });

                subscriber.Gateway = Enums.GatewayType.Stripe;
                subscriber.GatewayCustomerId = customer.Id;
                updatedSubscriber = true;
            }
            else
            {
                if(paymentToken.StartsWith("btok_"))
                {
                    await bankSerice.CreateAsync(customer.Id, new BankAccountCreateOptions
                    {
                        SourceToken = paymentToken
                    });
                }
                else
                {
                    await cardService.CreateAsync(customer.Id, new StripeCardCreateOptions
                    {
                        SourceToken = paymentToken
                    });
                }

                if(!string.IsNullOrWhiteSpace(customer.DefaultSourceId))
                {
                    var source = customer.Sources.FirstOrDefault(s => s.Id == customer.DefaultSourceId);
                    if(source.BankAccount != null)
                    {
                        await bankSerice.DeleteAsync(customer.Id, customer.DefaultSourceId);
                    }
                    else if(source.Card != null)
                    {
                        await cardService.DeleteAsync(customer.Id, customer.DefaultSourceId);
                    }
                }
            }

            return updatedSubscriber;
        }

        public async Task<BillingInfo.BillingInvoice> GetUpcomingInvoiceAsync(ISubscriber subscriber)
        {
            if(!string.IsNullOrWhiteSpace(subscriber.GatewaySubscriptionId))
            {
                var subscriptionService = new StripeSubscriptionService();
                var invoiceService = new StripeInvoiceService();
                var sub = await subscriptionService.GetAsync(subscriber.GatewaySubscriptionId);
                if(sub != null)
                {
                    if(!sub.CanceledAt.HasValue && !string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
                    {
                        try
                        {
                            var upcomingInvoice = await invoiceService.UpcomingAsync(subscriber.GatewayCustomerId);
                            if(upcomingInvoice != null)
                            {
                                return new BillingInfo.BillingInvoice(upcomingInvoice);
                            }
                        }
                        catch(StripeException) { }
                    }
                }
            }
            return null;
        }

        public async Task<BillingInfo> GetBillingAsync(ISubscriber subscriber)
        {
            var billingInfo = new BillingInfo();
            var customerService = new StripeCustomerService();
            var subscriptionService = new StripeSubscriptionService();
            var chargeService = new StripeChargeService();
            var invoiceService = new StripeInvoiceService();

            if(!string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
            {
                var customer = await customerService.GetAsync(subscriber.GatewayCustomerId);
                if(customer != null)
                {
                    if(!string.IsNullOrWhiteSpace(customer.DefaultSourceId) && customer.Sources?.Data != null)
                    {
                        if(customer.DefaultSourceId.StartsWith("card_"))
                        {
                            var source = customer.Sources.Data.FirstOrDefault(s => s.Card?.Id == customer.DefaultSourceId);
                            if(source != null)
                            {
                                billingInfo.PaymentSource = new BillingInfo.BillingSource(source);
                            }
                        }
                        else if(customer.DefaultSourceId.StartsWith("ba_"))
                        {
                            var source = customer.Sources.Data
                                .FirstOrDefault(s => s.BankAccount?.Id == customer.DefaultSourceId);
                            if(source != null)
                            {
                                billingInfo.PaymentSource = new BillingInfo.BillingSource(source);
                            }
                        }
                    }

                    var charges = await chargeService.ListAsync(new StripeChargeListOptions
                    {
                        CustomerId = customer.Id,
                        Limit = 20
                    });
                    billingInfo.Charges = charges?.Data?.OrderByDescending(c => c.Created)
                        .Select(c => new BillingInfo.BillingCharge(c));
                }
            }

            if(!string.IsNullOrWhiteSpace(subscriber.GatewaySubscriptionId))
            {
                var sub = await subscriptionService.GetAsync(subscriber.GatewaySubscriptionId);
                if(sub != null)
                {
                    billingInfo.Subscription = new BillingInfo.BillingSubscription(sub);
                }

                if(!sub.CanceledAt.HasValue && !string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
                {
                    try
                    {
                        var upcomingInvoice = await invoiceService.UpcomingAsync(subscriber.GatewayCustomerId);
                        if(upcomingInvoice != null)
                        {
                            billingInfo.UpcomingInvoice = new BillingInfo.BillingInvoice(upcomingInvoice);
                        }
                    }
                    catch(StripeException) { }
                }
            }

            return billingInfo;
        }
    }
}
