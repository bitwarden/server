using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Stripe;
using System.Collections.Generic;
using Bit.Core.Exceptions;
using System.Linq;
using Bit.Core.Models.Business;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services
{
    public class StripePaymentService : IPaymentService
    {
        private const string PremiumPlanId = "premium-annually";
        private const string StoragePlanId = "storage-gb-annually";

        private readonly ITransactionRepository _transactionRepository;
        private readonly ILogger<StripePaymentService> _logger;
        private readonly Braintree.BraintreeGateway _btGateway;

        public StripePaymentService(
            ITransactionRepository transactionRepository,
            GlobalSettings globalSettings,
            ILogger<StripePaymentService> logger)
        {
            _btGateway = new Braintree.BraintreeGateway
            {
                Environment = globalSettings.Braintree.Production ?
                    Braintree.Environment.PRODUCTION : Braintree.Environment.SANDBOX,
                MerchantId = globalSettings.Braintree.MerchantId,
                PublicKey = globalSettings.Braintree.PublicKey,
                PrivateKey = globalSettings.Braintree.PrivateKey
            };
            _transactionRepository = transactionRepository;
            _logger = logger;
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
                    Id = org.BraintreeCustomerIdPrefix() + org.Id.ToString("N").ToLower() + randomSuffix,
                    CustomFields = new Dictionary<string, string>
                    {
                        [org.BraintreeIdField()] = org.Id.ToString()
                    }
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
            if(paymentMethodType != PaymentMethodType.Credit && string.IsNullOrWhiteSpace(paymentToken))
            {
                throw new BadRequestException("Payment token is required.");
            }
            if(paymentMethodType == PaymentMethodType.Credit &&
                (user.Gateway != GatewayType.Stripe || string.IsNullOrWhiteSpace(user.GatewayCustomerId)))
            {
                throw new BadRequestException("Your account does not have any credit available.");
            }
            if(paymentMethodType == PaymentMethodType.BankAccount)
            {
                throw new GatewayException("Bank account payment method is not supported at this time.");
            }

            var invoiceService = new InvoiceService();
            var customerService = new CustomerService();

            var createdStripeCustomer = false;
            var addedCreditToStripeCustomer = false;
            Customer customer = null;
            Braintree.Transaction braintreeTransaction = null;
            Braintree.Customer braintreeCustomer = null;
            var stripePaymentMethod = paymentMethodType == PaymentMethodType.Card ||
                paymentMethodType == PaymentMethodType.BankAccount || paymentMethodType == PaymentMethodType.Credit;

            if(user.Gateway == GatewayType.Stripe && !string.IsNullOrWhiteSpace(user.GatewayCustomerId))
            {
                if(!string.IsNullOrWhiteSpace(paymentToken))
                {
                    try
                    {
                        await UpdatePaymentMethodAsync(user, paymentMethodType, paymentToken);
                    }
                    catch { }
                }
                try
                {
                    customer = await customerService.GetAsync(user.GatewayCustomerId);
                }
                catch { }
            }

            if(customer == null && !string.IsNullOrWhiteSpace(paymentToken))
            {
                string stipeCustomerSourceToken = null;
                var stripeCustomerMetadata = new Dictionary<string, string>();

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
                        Id = user.BraintreeCustomerIdPrefix() + user.Id.ToString("N").ToLower() + randomSuffix,
                        CustomFields = new Dictionary<string, string>
                        {
                            [user.BraintreeIdField()] = user.Id.ToString()
                        }
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

                customer = await customerService.CreateAsync(new CustomerCreateOptions
                {
                    Description = user.Name,
                    Email = user.Email,
                    SourceToken = stipeCustomerSourceToken,
                    Metadata = stripeCustomerMetadata
                });
                createdStripeCustomer = true;
            }

            if(customer == null)
            {
                throw new GatewayException("Could not set up customer payment profile.");
            }

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

                    if(previewInvoice.AmountDue > 0)
                    {
                        var braintreeCustomerId = customer.Metadata != null &&
                            customer.Metadata.ContainsKey("btCustomerId") ? customer.Metadata["btCustomerId"] : null;
                        if(!string.IsNullOrWhiteSpace(braintreeCustomerId))
                        {
                            var btInvoiceAmount = (previewInvoice.AmountDue / 100M);
                            var transactionResult = await _btGateway.Transaction.SaleAsync(
                                new Braintree.TransactionRequest
                                {
                                    Amount = btInvoiceAmount,
                                    CustomerId = braintreeCustomerId,
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

                            await customerService.UpdateAsync(customer.Id, new CustomerUpdateOptions
                            {
                                AccountBalance = -1 * previewInvoice.AmountDue
                            });
                            addedCreditToStripeCustomer = true;
                        }
                        else
                        {
                            throw new GatewayException("No payment was able to be collected.");
                        }
                    }
                }
                else if(paymentMethodType == PaymentMethodType.Credit)
                {
                    var previewInvoice = await invoiceService.UpcomingAsync(new UpcomingInvoiceOptions
                    {
                        CustomerId = customer.Id,
                        SubscriptionItems = ToInvoiceSubscriptionItemOptions(subCreateOptions.Items)
                    });
                    if(previewInvoice.AmountDue > 0)
                    {
                        throw new GatewayException("Your account does not have enough credit available.");
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
                if(customer != null)
                {
                    if(createdStripeCustomer)
                    {
                        await customerService.DeleteAsync(customer.Id);
                    }
                    else if(addedCreditToStripeCustomer)
                    {
                        await customerService.UpdateAsync(customer.Id, new CustomerUpdateOptions
                        {
                            AccountBalance = customer.AccountBalance
                        });
                    }
                }
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

                var customerService = new CustomerService();
                customerService.ExpandDefaultSource = true;
                var customer = await customerService.GetAsync(subscriber.GatewayCustomerId);

                var invoiceAmountDue = upcomingPreview.StartingBalance + invoiceAmount;
                if(invoiceAmountDue > 0 && !customer.Metadata.ContainsKey("btCustomerId"))
                {
                    if(customer.DefaultSource == null ||
                        (!(customer.DefaultSource is Card) && !(customer.DefaultSource is BankAccount)))
                    {
                        throw new BadRequestException("No payment method is available.");
                    }
                }

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
                        CustomerId = subscriber.GatewayCustomerId,
                        SubscriptionId = subscriber.GatewaySubscriptionId
                    });

                    var invoicePayOptions = new InvoicePayOptions();
                    if(invoice.AmountDue > 0)
                    {
                        if(customer?.Metadata?.ContainsKey("btCustomerId") ?? false)
                        {
                            invoicePayOptions.PaidOutOfBand = true;
                            var btInvoiceAmount = (invoice.AmountDue / 100M);
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

                    try
                    {
                        await invoiceService.PayAsync(invoice.Id, invoicePayOptions);
                    }
                    catch(StripeException)
                    {
                        throw new GatewayException("Unable to pay invoice.");
                    }
                }
                catch(Exception e)
                {
                    if(braintreeTransaction != null)
                    {
                        await _btGateway.Transaction.RefundAsync(braintreeTransaction.Id);
                    }
                    if(invoice != null)
                    {
                        await invoiceService.VoidInvoiceAsync(invoice.Id, new InvoiceVoidOptions());
                        if(invoice.StartingBalance != 0)
                        {
                            await customerService.UpdateAsync(customer.Id, new CustomerUpdateOptions
                            {
                                AccountBalance = customer.AccountBalance
                            });
                        }

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

            var hadBtCustomer = stripeCustomerMetadata.ContainsKey("btCustomerId");
            if(stripePaymentMethod)
            {
                stipeCustomerSourceToken = paymentToken;
            }
            else if(paymentMethodType == PaymentMethodType.PayPal)
            {
                if(hadBtCustomer)
                {
                    var pmResult = await _btGateway.PaymentMethod.CreateAsync(new Braintree.PaymentMethodRequest
                    {
                        CustomerId = stripeCustomerMetadata["btCustomerId"],
                        PaymentMethodNonce = paymentToken
                    });

                    if(pmResult.IsSuccess())
                    {
                        var customerResult = await _btGateway.Customer.UpdateAsync(
                            stripeCustomerMetadata["btCustomerId"], new Braintree.CustomerRequest
                            {
                                DefaultPaymentMethodToken = pmResult.Target.Token
                            });

                        if(customerResult.IsSuccess() && customerResult.Target.PaymentMethods.Length > 0)
                        {
                            braintreeCustomer = customerResult.Target;
                        }
                        else
                        {
                            await _btGateway.PaymentMethod.DeleteAsync(pmResult.Target.Token);
                            hadBtCustomer = false;
                        }
                    }
                    else
                    {
                        hadBtCustomer = false;
                    }
                }

                if(!hadBtCustomer)
                {
                    var customerResult = await _btGateway.Customer.CreateAsync(new Braintree.CustomerRequest
                    {
                        PaymentMethodNonce = paymentToken,
                        Email = subscriber.BillingEmailAddress(),
                        Id = subscriber.BraintreeCustomerIdPrefix() + subscriber.Id.ToString("N").ToLower() +
                            Utilities.CoreHelpers.RandomString(3, upper: false, numeric: false),
                        CustomFields = new Dictionary<string, string>
                        {
                            [subscriber.BraintreeIdField()] = subscriber.Id.ToString()
                        }
                    });

                    if(!customerResult.IsSuccess() || customerResult.Target.PaymentMethods.Length == 0)
                    {
                        throw new GatewayException("Failed to create PayPal customer record.");
                    }

                    braintreeCustomer = customerResult.Target;
                }
            }
            else
            {
                throw new GatewayException("Payment method is not supported at this time.");
            }

            if(stripeCustomerMetadata.ContainsKey("btCustomerId"))
            {
                if(braintreeCustomer?.Id != stripeCustomerMetadata["btCustomerId"])
                {
                    var nowSec = Utilities.CoreHelpers.ToEpocSeconds(DateTime.UtcNow);
                    stripeCustomerMetadata.Add($"btCustomerId_{nowSec}", stripeCustomerMetadata["btCustomerId"]);
                }
                stripeCustomerMetadata["btCustomerId"] = braintreeCustomer?.Id;
            }
            else if(!string.IsNullOrWhiteSpace(braintreeCustomer?.Id))
            {
                stripeCustomerMetadata.Add("btCustomerId", braintreeCustomer.Id);
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
                if(braintreeCustomer != null && !hadBtCustomer)
                {
                    await _btGateway.Customer.DeleteAsync(braintreeCustomer.Id);
                }
                throw e;
            }

            return createdCustomer;
        }

        public async Task<bool> CreditAccountAsync(ISubscriber subscriber, decimal creditAmount)
        {
            var customerService = new CustomerService();
            Customer customer = null;
            var customerExists = subscriber.Gateway == GatewayType.Stripe &&
                !string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId);
            if(customerExists)
            {
                customer = await customerService.GetAsync(subscriber.GatewayCustomerId);
            }
            else
            {
                customer = await customerService.CreateAsync(new CustomerCreateOptions
                {
                    Email = subscriber.BillingEmailAddress(),
                    Description = subscriber.BillingName(),
                });
                subscriber.Gateway = GatewayType.Stripe;
                subscriber.GatewayCustomerId = customer.Id;
            }
            await customerService.UpdateAsync(customer.Id, new CustomerUpdateOptions
            {
                AccountBalance = customer.AccountBalance - (long)(creditAmount * 100)
            });
            return !customerExists;
        }

        public async Task<BillingInfo> GetBillingAsync(ISubscriber subscriber)
        {
            var billingInfo = new BillingInfo();

            ICollection<Transaction> transactions = null;
            if(subscriber is User)
            {
                transactions = await _transactionRepository.GetManyByUserIdAsync(subscriber.Id);
            }
            else if(subscriber is Organization)
            {
                transactions = await _transactionRepository.GetManyByOrganizationIdAsync(subscriber.Id);
            }
            if(transactions != null)
            {
                billingInfo.Transactions = transactions?.OrderByDescending(i => i.CreationDate)
                    .Select(t => new BillingInfo.BillingTransaction(t));
            }

            var customerService = new CustomerService();
            var subscriptionService = new SubscriptionService();
            var chargeService = new ChargeService();
            var invoiceService = new InvoiceService();

            if(!string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
            {
                Customer customer = null;
                try
                {
                    customer = await customerService.GetAsync(subscriber.GatewayCustomerId);
                }
                catch(StripeException) { }
                if(customer != null)
                {
                    billingInfo.Balance = customer.AccountBalance / 100M;

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

                    var invoices = await invoiceService.ListAsync(new InvoiceListOptions
                    {
                        CustomerId = customer.Id,
                        Limit = 50
                    });
                    billingInfo.Invoices = invoices.Data.Where(i => i.Status != "void" && i.Status != "draft")
                        .OrderByDescending(i => i.Date).Select(i => new BillingInfo.BillingInvoice(i));
                }
            }

            return billingInfo;
        }

        public async Task<SubscriptionInfo> GetSubscriptionAsync(ISubscriber subscriber)
        {
            var subscriptionInfo = new SubscriptionInfo();
            var subscriptionService = new SubscriptionService();
            var invoiceService = new InvoiceService();

            if(!string.IsNullOrWhiteSpace(subscriber.GatewaySubscriptionId))
            {
                var sub = await subscriptionService.GetAsync(subscriber.GatewaySubscriptionId);
                if(sub != null)
                {
                    subscriptionInfo.Subscription = new SubscriptionInfo.BillingSubscription(sub);
                }

                if(!sub.CanceledAt.HasValue && !string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
                {
                    try
                    {
                        var upcomingInvoice = await invoiceService.UpcomingAsync(
                            new UpcomingInvoiceOptions { CustomerId = subscriber.GatewayCustomerId });
                        if(upcomingInvoice != null)
                        {
                            subscriptionInfo.UpcomingInvoice =
                                new SubscriptionInfo.BillingUpcomingInvoice(upcomingInvoice);
                        }
                    }
                    catch(StripeException) { }
                }
            }

            return subscriptionInfo;
        }
    }
}
