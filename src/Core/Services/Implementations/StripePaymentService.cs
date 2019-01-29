using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Stripe;
using System.Collections.Generic;
using Bit.Core.Exceptions;
using System.Linq;
using Bit.Core.Models.Business;
using Bit.Core.Enums;

namespace Bit.Core.Services
{
    public class StripePaymentService : IPaymentService
    {
        private const string PremiumPlanId = "premium-annually";
        private const string StoragePlanId = "storage-gb-annually";
        private readonly Braintree.BraintreeGateway _btGateway;

        public StripePaymentService(
            GlobalSettings globalSettings)
        {
            _btGateway = new Braintree.BraintreeGateway
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
            Braintree.Customer braintreeCustomer = null;
            Billing? stripeSubscriptionBilling = null;
            string stipeCustomerSourceToken = null;
            var stripeCustomerMetadata = new Dictionary<string, string>();

            if(paymentMethodType == PaymentMethodType.PayPal)
            {
                stripeSubscriptionBilling = Billing.SendInvoice;
                var randomSuffix = Utilities.CoreHelpers.RandomString(3, upper: false, numeric: false);
                var customerResult = await _btGateway.Customer.CreateAsync(new Braintree.CustomerRequest
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

            var customerService = new CustomerService();
            var customer = await customerService.CreateAsync(new CustomerCreateOptions
            {
                Description = user.Name,
                Email = user.Email,
                SourceToken = stipeCustomerSourceToken,
                Metadata = stripeCustomerMetadata
            });

            var subCreateOptions = new SubscriptionCreateOptions
            {
                CustomerId = customer.Id,
                Items = new List<SubscriptionItemOption>(),
                Billing = stripeSubscriptionBilling,
                DaysUntilDue = stripeSubscriptionBilling != null ? 1 : (long?)null,
                Metadata = new Dictionary<string, string>
                {
                    ["userId"] = user.Id.ToString()
                }
            };

            subCreateOptions.Items.Add(new SubscriptionItemOption
            {
                PlanId = PremiumPlanId,
                Quantity = 1
            });

            if(additionalStorageGb > 0)
            {
                subCreateOptions.Items.Add(new SubscriptionItemOption
                {
                    PlanId = StoragePlanId,
                    Quantity = additionalStorageGb
                });
            }

            Subscription subscription = null;
            try
            {
                var subscriptionService = new SubscriptionService();
                subscription = await subscriptionService.CreateAsync(subCreateOptions);

                if(stripeSubscriptionBilling == Billing.SendInvoice)
                {
                    var invoicePayOptions = new InvoicePayOptions();
                    var invoiceService = new InvoiceService();
                    var invoices = await invoiceService.ListAsync(new InvoiceListOptions
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
                        invoicePayOptions.PaidOutOfBand = true;
                        Braintree.Transaction braintreeTransaction = null;
                        try
                        {
                            var btInvoiceAmount = (invoice.AmountDue / 100M);
                            var transactionResult = await _btGateway.Transaction.SaleAsync(
                                new Braintree.TransactionRequest
                                {
                                    Amount = btInvoiceAmount,
                                    CustomerId = braintreeCustomer.Id,
                                    Options = new Braintree.TransactionOptionsRequest { SubmitForSettlement = true }
                                });

                            if(!transactionResult.IsSuccess())
                            {
                                throw new GatewayException("Failed to charge PayPal customer.");
                            }

                            braintreeTransaction = transactionResult.Target;
                            if(transactionResult.Target.Amount != btInvoiceAmount)
                            {
                                throw new GatewayException("PayPal charge mismatch.");
                            }

                            await invoiceService.UpdateAsync(invoice.Id, new InvoiceUpdateOptions
                            {
                                Metadata = new Dictionary<string, string>
                                {
                                    ["btTransactionId"] = braintreeTransaction.Id,
                                    ["btPayPalTransactionId"] = braintreeTransaction.PayPalDetails.AuthorizationId
                                }
                            });
                        }
                        catch(Exception e)
                        {
                            if(braintreeTransaction != null)
                            {
                                await _btGateway.Transaction.RefundAsync(braintreeTransaction.Id);
                            }
                            throw e;
                        }
                    }
                    else
                    {
                        throw new GatewayException("No payment was able to be collected.");
                    }

                    await invoiceService.PayAsync(invoice.Id, invoicePayOptions);
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
            var subscriptionItemService = new SubscriptionItemService();
            var subscriptionService = new SubscriptionService();
            var sub = await subscriptionService.GetAsync(storableSubscriber.GatewaySubscriptionId);
            if(sub == null)
            {
                throw new GatewayException("Subscription not found.");
            }

            var storageItem = sub.Items?.Data?.FirstOrDefault(i => i.Plan.Id == storagePlanId);
            if(additionalStorage > 0 && storageItem == null)
            {
                await subscriptionItemService.CreateAsync(new SubscriptionItemCreateOptions
                {
                    PlanId = storagePlanId,
                    Quantity = additionalStorage,
                    Prorate = true,
                    SubscriptionId = sub.Id
                });
            }
            else if(additionalStorage > 0 && storageItem != null)
            {
                await subscriptionItemService.UpdateAsync(storageItem.Id, new SubscriptionItemUpdateOptions
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
                var subscriptionService = new SubscriptionService();
                await subscriptionService.CancelAsync(subscriber.GatewaySubscriptionId,
                    new SubscriptionCancelOptions());
            }

            if(string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
            {
                return;
            }

            var chargeService = new ChargeService();
            var charges = await chargeService.ListAsync(new ChargeListOptions
            {
                CustomerId = subscriber.GatewayCustomerId
            });

            if(charges?.Data != null)
            {
                var refundService = new RefundService();
                foreach(var charge in charges.Data.Where(c => !c.Refunded))
                {
                    await refundService.CreateAsync(new RefundCreateOptions { ChargeId = charge.Id });
                }
            }

            var customerService = new CustomerService();
            await customerService.DeleteAsync(subscriber.GatewayCustomerId);
        }

        public async Task PreviewUpcomingInvoiceAndPayAsync(ISubscriber subscriber, string planId,
            int prorateThreshold = 500)
        {
            var invoiceService = new InvoiceService();
            var upcomingPreview = await invoiceService.UpcomingAsync(new UpcomingInvoiceOptions
            {
                CustomerId = subscriber.GatewayCustomerId,
                SubscriptionId = subscriber.GatewaySubscriptionId
            });

            var prorationAmount = upcomingPreview.Lines?.Data?
                .TakeWhile(i => i.Plan.Id == planId && i.Proration).Sum(i => i.Amount);
            if(prorationAmount.GetValueOrDefault() >= prorateThreshold)
            {
                try
                {
                    // Owes more than prorateThreshold on next invoice.
                    // Invoice them and pay now instead of waiting until next billing cycle.
                    var invoice = await invoiceService.CreateAsync(new InvoiceCreateOptions
                    {
                        CustomerId = subscriber.GatewayCustomerId,
                        SubscriptionId = subscriber.GatewaySubscriptionId
                    });

                    var invoicePayOptions = new InvoicePayOptions();
                    if(invoice.AmountDue > 0)
                    {
                        var customerService = new CustomerService();
                        var customer = await customerService.GetAsync(subscriber.GatewayCustomerId);
                        if(customer != null)
                        {
                            Braintree.Transaction braintreeTransaction = null;
                            if(customer.Metadata.ContainsKey("btCustomerId"))
                            {
                                invoicePayOptions.PaidOutOfBand = true;
                                try
                                {
                                    var btInvoiceAmount = (invoice.AmountDue / 100M);
                                    var transactionResult = await _btGateway.Transaction.SaleAsync(
                                        new Braintree.TransactionRequest
                                        {
                                            Amount = btInvoiceAmount,
                                            CustomerId = customer.Metadata["btCustomerId"],
                                            Options = new Braintree.TransactionOptionsRequest
                                            {
                                                SubmitForSettlement = true
                                            }
                                        });

                                    if(!transactionResult.IsSuccess())
                                    {
                                        throw new GatewayException("Failed to charge PayPal customer.");
                                    }

                                    braintreeTransaction = transactionResult.Target;
                                    if(transactionResult.Target.Amount != btInvoiceAmount)
                                    {
                                        throw new GatewayException("PayPal charge mismatch.");
                                    }

                                    await invoiceService.UpdateAsync(invoice.Id, new InvoiceUpdateOptions
                                    {
                                        Metadata = new Dictionary<string, string>
                                        {
                                            ["btTransactionId"] = braintreeTransaction.Id,
                                            ["btPayPalTransactionId"] =
                                                braintreeTransaction.PayPalDetails.AuthorizationId
                                        }
                                    });
                                }
                                catch(Exception e)
                                {
                                    if(braintreeTransaction != null)
                                    {
                                        await _btGateway.Transaction.RefundAsync(braintreeTransaction.Id);
                                    }
                                    throw e;
                                }
                            }
                        }
                    }

                    await invoiceService.PayAsync(invoice.Id, invoicePayOptions);
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

            var subscriptionService = new SubscriptionService();
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
                        new SubscriptionUpdateOptions { CancelAtPeriodEnd = true }) :
                    await subscriptionService.CancelAsync(sub.Id, new SubscriptionCancelOptions());
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

            var subscriptionService = new SubscriptionService();
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
                new SubscriptionUpdateOptions { CancelAtPeriodEnd = false });
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

            var cardService = new CardService();
            var bankSerice = new BankAccountService();
            var customerService = new CustomerService();
            Customer customer = null;

            if(!string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
            {
                customer = await customerService.GetAsync(subscriber.GatewayCustomerId);
            }

            if(customer == null)
            {
                customer = await customerService.CreateAsync(new CustomerCreateOptions
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
                    await cardService.CreateAsync(customer.Id, new CardCreateOptions
                    {
                        SourceToken = paymentToken
                    });
                }

                if(!string.IsNullOrWhiteSpace(customer.DefaultSourceId))
                {
                    var source = customer.Sources.FirstOrDefault(s => s.Id == customer.DefaultSourceId);
                    if(source is BankAccount)
                    {
                        await bankSerice.DeleteAsync(customer.Id, customer.DefaultSourceId);
                    }
                    else if(source is Card)
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
                var subscriptionService = new SubscriptionService();
                var invoiceService = new InvoiceService();
                var sub = await subscriptionService.GetAsync(subscriber.GatewaySubscriptionId);
                if(sub != null)
                {
                    if(!sub.CanceledAt.HasValue && !string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
                    {
                        try
                        {
                            var upcomingInvoice = await invoiceService.UpcomingAsync(new UpcomingInvoiceOptions
                            {
                                CustomerId = subscriber.GatewayCustomerId
                            });
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
            var customerService = new CustomerService();
            var subscriptionService = new SubscriptionService();
            var chargeService = new ChargeService();
            var invoiceService = new InvoiceService();

            if(!string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
            {
                var customer = await customerService.GetAsync(subscriber.GatewayCustomerId);
                if(customer != null)
                {
                    billingInfo.CreditAmount = customer.AccountBalance / 100M;

                    if(customer.Metadata?.ContainsKey("btCustomerId") ?? false)
                    {
                        var braintreeCustomer = await _btGateway.Customer.FindAsync(customer.Metadata["btCustomerId"]);
                        if(braintreeCustomer?.DefaultPaymentMethod != null)
                        {
                            billingInfo.PaymentSource = new BillingInfo.BillingSource(
                                braintreeCustomer.DefaultPaymentMethod);
                        }
                    }
                    else if(!string.IsNullOrWhiteSpace(customer.DefaultSourceId) && customer.Sources?.Data != null)
                    {
                        if(customer.DefaultSourceId.StartsWith("card_") || customer.DefaultSourceId.StartsWith("ba_"))
                        {
                            var source = customer.Sources.Data.FirstOrDefault(s =>
                                (s is Card || s is BankAccount) && s.Id == customer.DefaultSourceId);
                            if(source != null)
                            {
                                billingInfo.PaymentSource = new BillingInfo.BillingSource(source);
                            }
                        }
                    }

                    var charges = await chargeService.ListAsync(new ChargeListOptions
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
                        var upcomingInvoice = await invoiceService.UpcomingAsync(
                            new UpcomingInvoiceOptions { CustomerId = subscriber.GatewayCustomerId });
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
