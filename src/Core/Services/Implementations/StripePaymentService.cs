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

        public async Task PurchaseOrganizationAsync(Organization org, PaymentMethodType paymentMethodType,
            string paymentToken, Models.StaticStore.Plan plan, short additionalStorageGb,
            short additionalSeats, bool premiumAccessAddon)
        {
            var invoiceService = new InvoiceService();
            var customerService = new CustomerService();

            Braintree.Customer braintreeCustomer = null;
            string stipeCustomerSourceToken = null;
            var stripeCustomerMetadata = new Dictionary<string, string>();
            var stripePaymentMethod = paymentMethodType == PaymentMethodType.Card ||
                paymentMethodType == PaymentMethodType.BankAccount;

            if(stripePaymentMethod)
            {
                stipeCustomerSourceToken = paymentToken;
            }
            else if(paymentMethodType == PaymentMethodType.PayPal)
            {
                var randomSuffix = Utilities.CoreHelpers.RandomString(3, upper: false, numeric: false);
                var customerResult = await _btGateway.Customer.CreateAsync(new Braintree.CustomerRequest
                {
                    PaymentMethodNonce = paymentToken,
                    Email = org.BillingEmail,
                    Id = org.BraintreeCustomerIdPrefix() + org.Id.ToString("N").ToLower() + randomSuffix
                });

                if(!customerResult.IsSuccess() || customerResult.Target.PaymentMethods.Length == 0)
                {
                    throw new GatewayException("Failed to create PayPal customer record.");
                }

                braintreeCustomer = customerResult.Target;
                stripeCustomerMetadata.Add("btCustomerId", braintreeCustomer.Id);
            }
            else
            {
                throw new GatewayException("Payment method is not supported at this time.");
            }

            var subCreateOptions = new SubscriptionCreateOptions
            {
                TrialPeriodDays = plan.TrialPeriodDays,
                Items = new List<SubscriptionItemOption>(),
                Metadata = new Dictionary<string, string>
                {
                    [org.GatewayIdField()] = org.Id.ToString()
                }
            };

            if(plan.StripePlanId != null)
            {
                subCreateOptions.Items.Add(new SubscriptionItemOption
                {
                    PlanId = plan.StripePlanId,
                    Quantity = 1
                });
            }

            if(additionalSeats > 0 && plan.StripeSeatPlanId != null)
            {
                subCreateOptions.Items.Add(new SubscriptionItemOption
                {
                    PlanId = plan.StripeSeatPlanId,
                    Quantity = additionalSeats
                });
            }

            if(additionalStorageGb > 0)
            {
                subCreateOptions.Items.Add(new SubscriptionItemOption
                {
                    PlanId = plan.StripeStoragePlanId,
                    Quantity = additionalStorageGb
                });
            }

            if(premiumAccessAddon && plan.StripePremiumAccessPlanId != null)
            {
                subCreateOptions.Items.Add(new SubscriptionItemOption
                {
                    PlanId = plan.StripePremiumAccessPlanId,
                    Quantity = 1
                });
            }

            Customer customer = null;
            Subscription subscription = null;
            try
            {
                customer = await customerService.CreateAsync(new CustomerCreateOptions
                {
                    Description = org.BusinessName,
                    Email = org.BillingEmail,
                    SourceToken = stipeCustomerSourceToken,
                    Metadata = stripeCustomerMetadata
                });
                subCreateOptions.CustomerId = customer.Id;
                var subscriptionService = new SubscriptionService();
                subscription = await subscriptionService.CreateAsync(subCreateOptions);
            }
            catch(Exception e)
            {
                if(customer != null)
                {
                    await customerService.DeleteAsync(customer.Id);
                }
                if(braintreeCustomer != null)
                {
                    await _btGateway.Customer.DeleteAsync(braintreeCustomer.Id);
                }
                throw e;
            }

            org.Gateway = GatewayType.Stripe;
            org.GatewayCustomerId = customer.Id;
            org.GatewaySubscriptionId = subscription.Id;
            org.ExpirationDate = subscription.CurrentPeriodEnd;
        }

        public async Task PurchasePremiumAsync(User user, PaymentMethodType paymentMethodType, string paymentToken,
            short additionalStorageGb)
        {
            var invoiceService = new InvoiceService();
            var customerService = new CustomerService();

            Braintree.Transaction braintreeTransaction = null;
            Braintree.Customer braintreeCustomer = null;
            string stipeCustomerSourceToken = null;
            var stripeCustomerMetadata = new Dictionary<string, string>();
            var stripePaymentMethod = paymentMethodType == PaymentMethodType.Card ||
                paymentMethodType == PaymentMethodType.BankAccount;

            if(stripePaymentMethod)
            {
                stipeCustomerSourceToken = paymentToken;
            }
            else if(paymentMethodType == PaymentMethodType.PayPal)
            {
                var randomSuffix = Utilities.CoreHelpers.RandomString(3, upper: false, numeric: false);
                var customerResult = await _btGateway.Customer.CreateAsync(new Braintree.CustomerRequest
                {
                    PaymentMethodNonce = paymentToken,
                    Email = user.Email,
                    Id = user.BraintreeCustomerIdPrefix() + user.Id.ToString("N").ToLower() + randomSuffix
                });

                if(!customerResult.IsSuccess() || customerResult.Target.PaymentMethods.Length == 0)
                {
                    throw new GatewayException("Failed to create PayPal customer record.");
                }

                braintreeCustomer = customerResult.Target;
                stripeCustomerMetadata.Add("btCustomerId", braintreeCustomer.Id);
            }
            else
            {
                throw new GatewayException("Payment method is not supported at this time.");
            }

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
                Metadata = new Dictionary<string, string>
                {
                    [user.GatewayIdField()] = user.Id.ToString()
                }
            };

            subCreateOptions.Items.Add(new SubscriptionItemOption
            {
                PlanId = PremiumPlanId,
                Quantity = 1,
            });

            if(additionalStorageGb > 0)
            {
                subCreateOptions.Items.Add(new SubscriptionItemOption
                {
                    PlanId = StoragePlanId,
                    Quantity = additionalStorageGb
                });
            }

            var subInvoiceMetadata = new Dictionary<string, string>();
            Subscription subscription = null;
            try
            {
                if(!stripePaymentMethod)
                {
                    var previewInvoice = await invoiceService.UpcomingAsync(new UpcomingInvoiceOptions
                    {
                        CustomerId = customer.Id,
                        SubscriptionItems = ToInvoiceSubscriptionItemOptions(subCreateOptions.Items)
                    });

                    await customerService.UpdateAsync(customer.Id, new CustomerUpdateOptions
                    {
                        AccountBalance = -1 * previewInvoice.AmountDue
                    });

                    if(braintreeCustomer != null)
                    {
                        var btInvoiceAmount = (previewInvoice.AmountDue / 100M);
                        var transactionResult = await _btGateway.Transaction.SaleAsync(
                            new Braintree.TransactionRequest
                            {
                                Amount = btInvoiceAmount,
                                CustomerId = braintreeCustomer.Id,
                                Options = new Braintree.TransactionOptionsRequest
                                {
                                    SubmitForSettlement = true,
                                    PayPal = new Braintree.TransactionOptionsPayPalRequest
                                    {
                                        CustomField = $"{user.BraintreeIdField()}:{user.Id}"
                                    }
                                },
                                CustomFields = new Dictionary<string, string>
                                {
                                    [user.BraintreeIdField()] = user.Id.ToString()
                                }
                            });

                        if(!transactionResult.IsSuccess())
                        {
                            throw new GatewayException("Failed to charge PayPal customer.");
                        }

                        braintreeTransaction = transactionResult.Target;
                        subInvoiceMetadata.Add("btTransactionId", braintreeTransaction.Id);
                        subInvoiceMetadata.Add("btPayPalTransactionId",
                            braintreeTransaction.PayPalDetails.AuthorizationId);
                    }
                    else
                    {
                        throw new GatewayException("No payment was able to be collected.");
                    }
                }

                var subscriptionService = new SubscriptionService();
                subscription = await subscriptionService.CreateAsync(subCreateOptions);

                if(!stripePaymentMethod && subInvoiceMetadata.Any())
                {
                    var invoices = await invoiceService.ListAsync(new InvoiceListOptions
                    {
                        SubscriptionId = subscription.Id
                    });

                    var invoice = invoices?.FirstOrDefault();
                    if(invoice == null)
                    {
                        throw new GatewayException("Invoice not found.");
                    }

                    await invoiceService.UpdateAsync(invoice.Id, new InvoiceUpdateOptions
                    {
                        Metadata = subInvoiceMetadata
                    });
                }
            }
            catch(Exception e)
            {
                await customerService.DeleteAsync(customer.Id);
                if(braintreeTransaction != null)
                {
                    await _btGateway.Transaction.RefundAsync(braintreeTransaction.Id);
                }
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

        private List<InvoiceSubscriptionItemOptions> ToInvoiceSubscriptionItemOptions(
            List<SubscriptionItemOption> subItemOptions)
        {
            return subItemOptions.Select(si => new InvoiceSubscriptionItemOptions
            {
                PlanId = si.PlanId,
                Quantity = si.Quantity
            }).ToList();
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

            Func<bool, Task<SubscriptionItem>> subUpdateAction = null;
            var storageItem = sub.Items?.FirstOrDefault(i => i.Plan.Id == storagePlanId);
            var subItemOptions = sub.Items.Where(i => i.Plan.Id != storagePlanId)
                .Select(i => new InvoiceSubscriptionItemOptions
                {
                    Id = i.Id,
                    PlanId = i.Plan.Id,
                    Quantity = i.Quantity,
                }).ToList();

            if(additionalStorage > 0 && storageItem == null)
            {
                subItemOptions.Add(new InvoiceSubscriptionItemOptions
                {
                    PlanId = storagePlanId,
                    Quantity = additionalStorage,
                });
                subUpdateAction = (prorate) => subscriptionItemService.CreateAsync(
                    new SubscriptionItemCreateOptions
                    {
                        PlanId = storagePlanId,
                        Quantity = additionalStorage,
                        SubscriptionId = sub.Id,
                        Prorate = prorate
                    });
            }
            else if(additionalStorage > 0 && storageItem != null)
            {
                subItemOptions.Add(new InvoiceSubscriptionItemOptions
                {
                    Id = storageItem.Id,
                    PlanId = storagePlanId,
                    Quantity = additionalStorage,
                });
                subUpdateAction = (prorate) => subscriptionItemService.UpdateAsync(storageItem.Id,
                    new SubscriptionItemUpdateOptions
                    {
                        PlanId = storagePlanId,
                        Quantity = additionalStorage,
                        Prorate = prorate
                    });
            }
            else if(additionalStorage == 0 && storageItem != null)
            {
                subItemOptions.Add(new InvoiceSubscriptionItemOptions
                {
                    Id = storageItem.Id,
                    Deleted = true
                });
                subUpdateAction = (prorate) => subscriptionItemService.DeleteAsync(storageItem.Id);
            }

            var invoicedNow = false;
            if(additionalStorage > 0)
            {
                invoicedNow = await PreviewUpcomingInvoiceAndPayAsync(
                    storableSubscriber, storagePlanId, subItemOptions, 400);
            }

            await subUpdateAction(!invoicedNow);
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

            var customerService = new CustomerService();
            var customer = await customerService.GetAsync(subscriber.GatewayCustomerId);
            if(customer == null)
            {
                return;
            }

            if(customer.Metadata.ContainsKey("btCustomerId"))
            {
                var transactionRequest = new Braintree.TransactionSearchRequest()
                    .CustomerId.Is(customer.Metadata["btCustomerId"]);
                var transactions = _btGateway.Transaction.Search(transactionRequest);

                if((transactions?.MaximumCount ?? 0) > 0)
                {
                    var txs = transactions.Cast<Braintree.Transaction>().Where(c => c.RefundedTransactionId == null);
                    foreach(var transaction in txs)
                    {
                        await _btGateway.Transaction.RefundAsync(transaction.Id);
                    }
                }

                await _btGateway.Customer.DeleteAsync(customer.Metadata["btCustomerId"]);
            }
            else
            {
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
            }

            await customerService.DeleteAsync(subscriber.GatewayCustomerId);
        }

        public async Task<bool> PreviewUpcomingInvoiceAndPayAsync(ISubscriber subscriber, string planId,
            List<InvoiceSubscriptionItemOptions> subItemOptions, int prorateThreshold = 500)
        {
            var invoiceService = new InvoiceService();
            var invoiceItemService = new InvoiceItemService();

            var pendingInvoiceItems = invoiceItemService.ListAutoPaging(new InvoiceItemListOptions
            {
                CustomerId = subscriber.GatewayCustomerId
            }).ToList().Where(i => i.InvoiceId == null);
            var pendingInvoiceItemsDict = pendingInvoiceItems.ToDictionary(pii => pii.Id);

            var upcomingPreview = await invoiceService.UpcomingAsync(new UpcomingInvoiceOptions
            {
                CustomerId = subscriber.GatewayCustomerId,
                SubscriptionId = subscriber.GatewaySubscriptionId,
                SubscriptionItems = subItemOptions
            });

            var itemsForInvoice = upcomingPreview.Lines?.Data?
                .Where(i => pendingInvoiceItemsDict.ContainsKey(i.Id) || (i.Plan.Id == planId && i.Proration));
            var invoiceAmount = itemsForInvoice?.Sum(i => i.Amount) ?? 0;
            var invoiceNow = invoiceAmount >= prorateThreshold;
            if(invoiceNow)
            {
                // Owes more than prorateThreshold on next invoice.
                // Invoice them and pay now instead of waiting until next billing cycle.

                Invoice invoice = null;
                var createdInvoiceItems = new List<InvoiceItem>();
                Braintree.Transaction braintreeTransaction = null;
                try
                {
                    foreach(var ii in itemsForInvoice)
                    {
                        if(pendingInvoiceItemsDict.ContainsKey(ii.Id))
                        {
                            continue;
                        }
                        var invoiceItem = await invoiceItemService.CreateAsync(new InvoiceItemCreateOptions
                        {
                            Currency = ii.Currency,
                            Description = ii.Description,
                            CustomerId = subscriber.GatewayCustomerId,
                            SubscriptionId = ii.SubscriptionId,
                            Discountable = ii.Discountable,
                            Amount = ii.Amount
                        });
                        createdInvoiceItems.Add(invoiceItem);
                    }

                    invoice = await invoiceService.CreateAsync(new InvoiceCreateOptions
                    {
                        Billing = Billing.SendInvoice,
                        DaysUntilDue = 1,
                        CustomerId = subscriber.GatewayCustomerId
                    });

                    var invoicePayOptions = new InvoicePayOptions();
                    var customerService = new CustomerService();
                    var customer = await customerService.GetAsync(subscriber.GatewayCustomerId);
                    if(customer != null)
                    {
                        if(customer.Metadata.ContainsKey("btCustomerId"))
                        {
                            invoicePayOptions.PaidOutOfBand = true;
                            var btInvoiceAmount = (invoiceAmount / 100M);
                            var transactionResult = await _btGateway.Transaction.SaleAsync(
                                new Braintree.TransactionRequest
                                {
                                    Amount = btInvoiceAmount,
                                    CustomerId = customer.Metadata["btCustomerId"],
                                    Options = new Braintree.TransactionOptionsRequest
                                    {
                                        SubmitForSettlement = true,
                                        PayPal = new Braintree.TransactionOptionsPayPalRequest
                                        {
                                            CustomField = $"{subscriber.BraintreeIdField()}:{subscriber.Id}"
                                        }
                                    },
                                    CustomFields = new Dictionary<string, string>
                                    {
                                        [subscriber.BraintreeIdField()] = subscriber.Id.ToString()
                                    }
                                });

                            if(!transactionResult.IsSuccess())
                            {
                                throw new GatewayException("Failed to charge PayPal customer.");
                            }

                            braintreeTransaction = transactionResult.Target;
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
                    }

                    await invoiceService.PayAsync(invoice.Id, invoicePayOptions);
                }
                catch(Exception e)
                {
                    if(braintreeTransaction != null)
                    {
                        await _btGateway.Transaction.RefundAsync(braintreeTransaction.Id);
                    }
                    if(invoice != null)
                    {
                        await invoiceService.DeleteAsync(invoice.Id);

                        // Restore invoice items that were brought in
                        foreach(var item in pendingInvoiceItems)
                        {
                            var i = new InvoiceItemCreateOptions
                            {
                                Currency = item.Currency,
                                Description = item.Description,
                                CustomerId = item.CustomerId,
                                SubscriptionId = item.SubscriptionId,
                                Discountable = item.Discountable,
                                Metadata = item.Metadata,
                                Quantity = item.Quantity,
                                UnitAmount = item.UnitAmount
                            };
                            await invoiceItemService.CreateAsync(i);
                        }
                    }
                    else
                    {
                        foreach(var ii in createdInvoiceItems)
                        {
                            await invoiceItemService.DeleteAsync(ii.Id);
                        }
                    }
                    throw e;
                }
            }
            return invoiceNow;
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

        public async Task<bool> UpdatePaymentMethodAsync(ISubscriber subscriber, PaymentMethodType paymentMethodType,
            string paymentToken)
        {
            if(subscriber == null)
            {
                throw new ArgumentNullException(nameof(subscriber));
            }

            if(subscriber.Gateway.HasValue && subscriber.Gateway.Value != GatewayType.Stripe)
            {
                throw new GatewayException("Switching from one payment type to another is not supported. " +
                    "Contact us for assistance.");
            }

            var createdCustomer = false;
            Braintree.Customer braintreeCustomer = null;
            string stipeCustomerSourceToken = null;
            var stripeCustomerMetadata = new Dictionary<string, string>();
            var stripePaymentMethod = paymentMethodType == PaymentMethodType.Card ||
                paymentMethodType == PaymentMethodType.BankAccount;

            var cardService = new CardService();
            var bankSerice = new BankAccountService();
            var customerService = new CustomerService();
            Customer customer = null;

            if(!string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
            {
                customer = await customerService.GetAsync(subscriber.GatewayCustomerId);
                if(customer.Metadata?.Any() ?? false)
                {
                    stripeCustomerMetadata = customer.Metadata;
                }
            }

            if(stripeCustomerMetadata.ContainsKey("btCustomerId"))
            {
                var nowSec = Utilities.CoreHelpers.ToEpocSeconds(DateTime.UtcNow);
                stripeCustomerMetadata.Add($"btCustomerId_{nowSec}", stripeCustomerMetadata["btCustomerId"]);
                stripeCustomerMetadata["btCustomerId"] = null;
            }

            if(stripePaymentMethod)
            {
                stipeCustomerSourceToken = paymentToken;
            }
            else if(paymentMethodType == PaymentMethodType.PayPal)
            {
                var randomSuffix = Utilities.CoreHelpers.RandomString(3, upper: false, numeric: false);
                var customerResult = await _btGateway.Customer.CreateAsync(new Braintree.CustomerRequest
                {
                    PaymentMethodNonce = paymentToken,
                    Email = subscriber.BillingEmailAddress(),
                    Id = subscriber.BraintreeCustomerIdPrefix() + subscriber.Id.ToString("N").ToLower() + randomSuffix
                });

                if(!customerResult.IsSuccess() || customerResult.Target.PaymentMethods.Length == 0)
                {
                    throw new GatewayException("Failed to create PayPal customer record.");
                }

                braintreeCustomer = customerResult.Target;
                if(stripeCustomerMetadata.ContainsKey("btCustomerId"))
                {
                    stripeCustomerMetadata["btCustomerId"] = braintreeCustomer.Id;
                }
                else
                {
                    stripeCustomerMetadata.Add("btCustomerId", braintreeCustomer.Id);
                }
            }
            else
            {
                throw new GatewayException("Payment method is not supported at this time.");
            }

            try
            {
                if(customer == null)
                {
                    customer = await customerService.CreateAsync(new CustomerCreateOptions
                    {
                        Description = subscriber.BillingName(),
                        Email = subscriber.BillingEmailAddress(),
                        SourceToken = stipeCustomerSourceToken,
                        Metadata = stripeCustomerMetadata
                    });

                    subscriber.Gateway = GatewayType.Stripe;
                    subscriber.GatewayCustomerId = customer.Id;
                    createdCustomer = true;
                }

                if(!createdCustomer)
                {
                    string defaultSourceId = null;
                    if(stripePaymentMethod)
                    {
                        if(paymentToken.StartsWith("btok_"))
                        {
                            var bankAccount = await bankSerice.CreateAsync(customer.Id, new BankAccountCreateOptions
                            {
                                SourceToken = paymentToken
                            });
                            defaultSourceId = bankAccount.Id;
                        }
                        else
                        {
                            var card = await cardService.CreateAsync(customer.Id, new CardCreateOptions
                            {
                                SourceToken = paymentToken,
                            });
                            defaultSourceId = card.Id;
                        }
                    }

                    foreach(var source in customer.Sources.Where(s => s.Id != defaultSourceId))
                    {
                        if(source is BankAccount)
                        {
                            await bankSerice.DeleteAsync(customer.Id, source.Id);
                        }
                        else if(source is Card)
                        {
                            await cardService.DeleteAsync(customer.Id, source.Id);
                        }
                    }

                    customer = await customerService.UpdateAsync(customer.Id, new CustomerUpdateOptions
                    {
                        Metadata = stripeCustomerMetadata,
                        DefaultSource = defaultSourceId
                    });
                }
            }
            catch(Exception e)
            {
                if(braintreeCustomer != null)
                {
                    await _btGateway.Customer.DeleteAsync(braintreeCustomer.Id);
                }
                throw e;
            }

            return createdCustomer;
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
                        try
                        {
                            var braintreeCustomer = await _btGateway.Customer.FindAsync(
                                customer.Metadata["btCustomerId"]);
                            if(braintreeCustomer?.DefaultPaymentMethod != null)
                            {
                                billingInfo.PaymentSource = new BillingInfo.BillingSource(
                                    braintreeCustomer.DefaultPaymentMethod);
                            }
                        }
                        catch(Braintree.Exceptions.NotFoundException) { }
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
