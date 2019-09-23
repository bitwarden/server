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
using Bit.Billing.Models;

namespace Bit.Core.Services
{
    public class StripePaymentService : IPaymentService
    {
        private const string PremiumPlanId = "premium-annually";
        private const string PremiumPlanAppleIapId = "premium-annually-appleiap";
        private const decimal PremiumPlanAppleIapPrice = 14.99M;
        private const string StoragePlanId = "storage-gb-annually";

        private readonly ITransactionRepository _transactionRepository;
        private readonly IUserRepository _userRepository;
        private readonly IAppleIapService _appleIapService;
        private readonly ILogger<StripePaymentService> _logger;
        private readonly Braintree.BraintreeGateway _btGateway;

        public StripePaymentService(
            ITransactionRepository transactionRepository,
            IUserRepository userRepository,
            GlobalSettings globalSettings,
            IAppleIapService appleIapService,
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
            _userRepository = userRepository;
            _appleIapService = appleIapService;
            _logger = logger;
        }

        public async Task<string> PurchaseOrganizationAsync(Organization org, PaymentMethodType paymentMethodType,
            string paymentToken, Models.StaticStore.Plan plan, short additionalStorageGb,
            short additionalSeats, bool premiumAccessAddon)
        {
            var customerService = new CustomerService();

            Braintree.Customer braintreeCustomer = null;
            string stipeCustomerSourceToken = null;
            string stipeCustomerPaymentMethodId = null;
            var stripeCustomerMetadata = new Dictionary<string, string>();
            var stripePaymentMethod = paymentMethodType == PaymentMethodType.Card ||
                paymentMethodType == PaymentMethodType.BankAccount;

            if(stripePaymentMethod && !string.IsNullOrWhiteSpace(paymentToken))
            {
                if(paymentToken.StartsWith("pm_"))
                {
                    stipeCustomerPaymentMethodId = paymentToken;
                }
                else
                {
                    stipeCustomerSourceToken = paymentToken;
                }
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
                OffSession = true,
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
                    Source = stipeCustomerSourceToken,
                    PaymentMethodId = stipeCustomerPaymentMethodId,
                    Metadata = stripeCustomerMetadata,
                    InvoiceSettings = new CustomerInvoiceSettingsOptions
                    {
                        DefaultPaymentMethodId = stipeCustomerPaymentMethodId
                    }
                });
                subCreateOptions.AddExpand("latest_invoice.payment_intent");
                subCreateOptions.CustomerId = customer.Id;
                var subscriptionService = new SubscriptionService();
                subscription = await subscriptionService.CreateAsync(subCreateOptions);
                if(subscription.Status == "incomplete" && subscription.LatestInvoice?.PaymentIntent != null)
                {
                    if(subscription.LatestInvoice.PaymentIntent.Status == "requires_payment_method")
                    {
                        await subscriptionService.CancelAsync(subscription.Id, new SubscriptionCancelOptions());
                        throw new GatewayException("Payment method was declined.");
                    }
                }
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

            if(subscription.Status == "incomplete" &&
                subscription.LatestInvoice?.PaymentIntent?.Status == "requires_action")
            {
                org.Enabled = false;
                return subscription.LatestInvoice.PaymentIntent.ClientSecret;
            }
            else
            {
                org.Enabled = true;
                org.ExpirationDate = subscription.CurrentPeriodEnd;
                return null;
            }
        }

        public async Task<string> UpgradeFreeOrganizationAsync(Organization org, Models.StaticStore.Plan plan,
            short additionalStorageGb, short additionalSeats, bool premiumAccessAddon)
        {
            if(!string.IsNullOrWhiteSpace(org.GatewaySubscriptionId))
            {
                throw new BadRequestException("Organization already has a subscription.");
            }

            var customerService = new CustomerService();
            var customerOptions = new CustomerGetOptions();
            customerOptions.AddExpand("default_source");
            customerOptions.AddExpand("invoice_settings.default_payment_method");
            var customer = await customerService.GetAsync(org.GatewayCustomerId, customerOptions);
            if(customer == null)
            {
                throw new GatewayException("Could not find customer payment profile.");
            }

            var subCreateOptions = new SubscriptionCreateOptions
            {
                CustomerId = customer.Id,
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

            var stripePaymentMethod = false;
            var paymentMethodType = PaymentMethodType.Credit;
            var hasBtCustomerId = customer.Metadata.ContainsKey("btCustomerId");
            if(hasBtCustomerId)
            {
                paymentMethodType = PaymentMethodType.PayPal;
            }
            else
            {
                if(customer.InvoiceSettings?.DefaultPaymentMethod?.Type == "card")
                {
                    paymentMethodType = PaymentMethodType.Card;
                    stripePaymentMethod = true;
                }
                else if(customer.DefaultSource != null)
                {
                    if(customer.DefaultSource is Card || customer.DefaultSource is SourceCard)
                    {
                        paymentMethodType = PaymentMethodType.Card;
                        stripePaymentMethod = true;
                    }
                    else if(customer.DefaultSource is BankAccount || customer.DefaultSource is SourceAchDebit)
                    {
                        paymentMethodType = PaymentMethodType.BankAccount;
                        stripePaymentMethod = true;
                    }
                }
                else
                {
                    var paymentMethod = GetLatestCardPaymentMethod(customer.Id);
                    if(paymentMethod != null)
                    {
                        paymentMethodType = PaymentMethodType.Card;
                        stripePaymentMethod = true;
                        subCreateOptions.DefaultPaymentMethodId = paymentMethod.Id;
                    }
                }
            }

            var subscription = await ChargeForNewSubscriptionAsync(org, customer, false,
                stripePaymentMethod, paymentMethodType, subCreateOptions, null);
            org.GatewaySubscriptionId = subscription.Id;

            if(subscription.Status == "incomplete" &&
                subscription.LatestInvoice?.PaymentIntent?.Status == "requires_action")
            {
                org.Enabled = false;
                return subscription.LatestInvoice.PaymentIntent.ClientSecret;
            }
            else
            {
                org.Enabled = true;
                org.ExpirationDate = subscription.CurrentPeriodEnd;
                return null;
            }
        }

        public async Task<string> PurchasePremiumAsync(User user, PaymentMethodType paymentMethodType,
            string paymentToken, short additionalStorageGb)
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
            if(paymentMethodType == PaymentMethodType.BankAccount || paymentMethodType == PaymentMethodType.GoogleInApp)
            {
                throw new GatewayException("Payment method is not supported at this time.");
            }
            if((paymentMethodType == PaymentMethodType.GoogleInApp ||
                paymentMethodType == PaymentMethodType.AppleInApp) && additionalStorageGb > 0)
            {
                throw new BadRequestException("You cannot add storage with this payment method.");
            }

            var customerService = new CustomerService();
            var createdStripeCustomer = false;
            Customer customer = null;
            Braintree.Customer braintreeCustomer = null;
            var stripePaymentMethod = paymentMethodType == PaymentMethodType.Card ||
                paymentMethodType == PaymentMethodType.BankAccount || paymentMethodType == PaymentMethodType.Credit;

            string stipeCustomerPaymentMethodId = null;
            string stipeCustomerSourceToken = null;
            if(stripePaymentMethod && !string.IsNullOrWhiteSpace(paymentToken))
            {
                if(paymentToken.StartsWith("pm_"))
                {
                    stipeCustomerPaymentMethodId = paymentToken;
                }
                else
                {
                    stipeCustomerSourceToken = paymentToken;
                }
            }

            if(user.Gateway == GatewayType.Stripe && !string.IsNullOrWhiteSpace(user.GatewayCustomerId))
            {
                if(!string.IsNullOrWhiteSpace(paymentToken))
                {
                    try
                    {
                        await UpdatePaymentMethodAsync(user, paymentMethodType, paymentToken, true);
                    }
                    catch(Exception e)
                    {
                        var message = e.Message.ToLowerInvariant();
                        if(message.Contains("apple") || message.Contains("in-app"))
                        {
                            throw e;
                        }
                    }
                }
                try
                {
                    customer = await customerService.GetAsync(user.GatewayCustomerId);
                }
                catch { }
            }

            if(customer == null && !string.IsNullOrWhiteSpace(paymentToken))
            {
                var stripeCustomerMetadata = new Dictionary<string, string>();
                if(paymentMethodType == PaymentMethodType.PayPal)
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
                else if(paymentMethodType == PaymentMethodType.AppleInApp)
                {
                    var verifiedReceiptStatus = await _appleIapService.GetVerifiedReceiptStatusAsync(paymentToken);
                    if(verifiedReceiptStatus == null)
                    {
                        throw new GatewayException("Cannot verify apple in-app purchase.");
                    }
                    var receiptOriginalTransactionId = verifiedReceiptStatus.GetOriginalTransactionId();
                    await VerifyAppleReceiptNotInUseAsync(receiptOriginalTransactionId, user);
                    await _appleIapService.SaveReceiptAsync(verifiedReceiptStatus, user.Id);
                    stripeCustomerMetadata.Add("appleReceipt", receiptOriginalTransactionId);
                }
                else if(!stripePaymentMethod)
                {
                    throw new GatewayException("Payment method is not supported at this time.");
                }

                customer = await customerService.CreateAsync(new CustomerCreateOptions
                {
                    Description = user.Name,
                    Email = user.Email,
                    Metadata = stripeCustomerMetadata,
                    PaymentMethodId = stipeCustomerPaymentMethodId,
                    Source = stipeCustomerSourceToken,
                    InvoiceSettings = new CustomerInvoiceSettingsOptions
                    {
                        DefaultPaymentMethodId = stipeCustomerPaymentMethodId
                    }
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
                PlanId = paymentMethodType == PaymentMethodType.AppleInApp ? PremiumPlanAppleIapId : PremiumPlanId,
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

            var subscription = await ChargeForNewSubscriptionAsync(user, customer, createdStripeCustomer,
                stripePaymentMethod, paymentMethodType, subCreateOptions, braintreeCustomer);

            user.Gateway = GatewayType.Stripe;
            user.GatewayCustomerId = customer.Id;
            user.GatewaySubscriptionId = subscription.Id;

            if(subscription.Status == "incomplete" &&
                subscription.LatestInvoice?.PaymentIntent?.Status == "requires_action")
            {
                return subscription.LatestInvoice.PaymentIntent.ClientSecret;
            }
            else
            {
                user.Premium = true;
                user.PremiumExpirationDate = subscription.CurrentPeriodEnd;
                return null;
            }
        }

        private async Task<Subscription> ChargeForNewSubscriptionAsync(ISubscriber subcriber, Customer customer,
            bool createdStripeCustomer, bool stripePaymentMethod, PaymentMethodType paymentMethodType,
            SubscriptionCreateOptions subCreateOptions, Braintree.Customer braintreeCustomer)
        {
            var addedCreditToStripeCustomer = false;
            Braintree.Transaction braintreeTransaction = null;
            Transaction appleTransaction = null;
            var invoiceService = new InvoiceService();
            var customerService = new CustomerService();

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
                        var appleReceiptOrigTransactionId = customer.Metadata != null &&
                            customer.Metadata.ContainsKey("appleReceipt") ? customer.Metadata["appleReceipt"] : null;
                        var braintreeCustomerId = customer.Metadata != null &&
                            customer.Metadata.ContainsKey("btCustomerId") ? customer.Metadata["btCustomerId"] : null;
                        if(!string.IsNullOrWhiteSpace(appleReceiptOrigTransactionId))
                        {
                            if(!subcriber.IsUser())
                            {
                                throw new GatewayException("In-app purchase is only allowed for users.");
                            }

                            var appleReceipt = await _appleIapService.GetReceiptAsync(
                                appleReceiptOrigTransactionId);
                            var verifiedAppleReceipt = await _appleIapService.GetVerifiedReceiptStatusAsync(
                                appleReceipt.Item1);
                            if(verifiedAppleReceipt == null)
                            {
                                throw new GatewayException("Failed to get Apple in-app purchase receipt data.");
                            }
                            subInvoiceMetadata.Add("appleReceipt", verifiedAppleReceipt.GetOriginalTransactionId());
                            var lastTransactionId = verifiedAppleReceipt.GetLastTransactionId();
                            subInvoiceMetadata.Add("appleReceiptTransactionId", lastTransactionId);
                            var existingTransaction = await _transactionRepository.GetByGatewayIdAsync(
                                GatewayType.AppStore, lastTransactionId);
                            if(existingTransaction == null)
                            {
                                appleTransaction = verifiedAppleReceipt.BuildTransactionFromLastTransaction(
                                    PremiumPlanAppleIapPrice, subcriber.Id);
                                appleTransaction.Type = TransactionType.Charge;
                                await _transactionRepository.CreateAsync(appleTransaction);
                            }
                        }
                        else if(!string.IsNullOrWhiteSpace(braintreeCustomerId))
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
                                            CustomField = $"{subcriber.BraintreeIdField()}:{subcriber.Id}"
                                        }
                                    },
                                    CustomFields = new Dictionary<string, string>
                                    {
                                        [subcriber.BraintreeIdField()] = subcriber.Id.ToString()
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

                        await customerService.UpdateAsync(customer.Id, new CustomerUpdateOptions
                        {
                            Balance = customer.Balance - previewInvoice.AmountDue
                        });
                        addedCreditToStripeCustomer = true;
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

                subCreateOptions.OffSession = true;
                subCreateOptions.AddExpand("latest_invoice.payment_intent");
                var subscriptionService = new SubscriptionService();
                subscription = await subscriptionService.CreateAsync(subCreateOptions);
                if(subscription.Status == "incomplete" && subscription.LatestInvoice?.PaymentIntent != null)
                {
                    if(subscription.LatestInvoice.PaymentIntent.Status == "requires_payment_method")
                    {
                        await subscriptionService.CancelAsync(subscription.Id, new SubscriptionCancelOptions());
                        throw new GatewayException("Payment method was declined.");
                    }
                }

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

                return subscription;
            }
            catch(Exception e)
            {
                if(customer != null)
                {
                    if(createdStripeCustomer)
                    {
                        await customerService.DeleteAsync(customer.Id);
                    }
                    else if(addedCreditToStripeCustomer || customer.Balance < 0)
                    {
                        await customerService.UpdateAsync(customer.Id, new CustomerUpdateOptions
                        {
                            Balance = customer.Balance
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
                if(appleTransaction != null)
                {
                    await _transactionRepository.DeleteAsync(appleTransaction);
                }

                if(e is StripeException strEx &&
                    (strEx.StripeError?.Message?.Contains("cannot be used because it is not verified") ?? false))
                {
                    throw new GatewayException("Bank account is not yet verified.");
                }

                throw e;
            }
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

        public async Task<string> AdjustStorageAsync(IStorableSubscriber storableSubscriber, int additionalStorage,
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

            string paymentIntentClientSecret = null;
            var invoicedNow = false;
            if(additionalStorage > 0)
            {
                var result = await PreviewUpcomingInvoiceAndPayAsync(
                    storableSubscriber, storagePlanId, subItemOptions, 400);
                invoicedNow = result.Item1;
                paymentIntentClientSecret = result.Item2;
            }

            if(subUpdateAction != null)
            {
                await subUpdateAction(!invoicedNow);
            }
            return paymentIntentClientSecret;
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
                    foreach(var charge in charges.Data.Where(c => c.Captured.GetValueOrDefault() && !c.Refunded))
                    {
                        await refundService.CreateAsync(new RefundCreateOptions { ChargeId = charge.Id });
                    }
                }
            }

            await customerService.DeleteAsync(subscriber.GatewayCustomerId);
        }

        public async Task<Tuple<bool, string>> PreviewUpcomingInvoiceAndPayAsync(ISubscriber subscriber, string planId,
            List<InvoiceSubscriptionItemOptions> subItemOptions, int prorateThreshold = 500)
        {
            var customerService = new CustomerService();
            var customerOptions = new CustomerGetOptions();
            customerOptions.AddExpand("default_source");
            customerOptions.AddExpand("invoice_settings.default_payment_method");
            var customer = await customerService.GetAsync(subscriber.GatewayCustomerId, customerOptions);
            var usingInAppPaymentMethod = customer.Metadata.ContainsKey("appleReceipt");
            if(usingInAppPaymentMethod)
            {
                throw new BadRequestException("Cannot perform this action with in-app purchase payment method. " +
                    "Contact support.");
            }

            var invoiceService = new InvoiceService();
            var invoiceItemService = new InvoiceItemService();
            string paymentIntentClientSecret = null;

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

                string cardPaymentMethodId = null;
                var invoiceAmountDue = upcomingPreview.StartingBalance + invoiceAmount;
                if(invoiceAmountDue > 0 && !customer.Metadata.ContainsKey("btCustomerId"))
                {
                    var hasDefaultCardPaymentMethod = customer.InvoiceSettings?.DefaultPaymentMethod?.Type == "card";
                    var hasDefaultValidSource = customer.DefaultSource != null &&
                        (customer.DefaultSource is Card || customer.DefaultSource is BankAccount);
                    if(!hasDefaultCardPaymentMethod && !hasDefaultValidSource)
                    {
                        cardPaymentMethodId = GetLatestCardPaymentMethod(customer.Id)?.Id;
                        if(cardPaymentMethodId == null)
                        {
                            throw new BadRequestException("No payment method is available.");
                        }
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
                        CollectionMethod = "send_invoice",
                        DaysUntilDue = 1,
                        CustomerId = subscriber.GatewayCustomerId,
                        SubscriptionId = subscriber.GatewaySubscriptionId,
                        DefaultPaymentMethodId = cardPaymentMethodId
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
                        else
                        {
                            invoicePayOptions.OffSession = true;
                            invoicePayOptions.PaymentMethodId = cardPaymentMethodId;
                        }
                    }

                    try
                    {
                        await invoiceService.PayAsync(invoice.Id, invoicePayOptions);
                    }
                    catch(StripeException e)
                    {
                        if(e.HttpStatusCode == System.Net.HttpStatusCode.PaymentRequired &&
                            e.StripeError?.Code == "invoice_payment_intent_requires_action")
                        {
                            // SCA required, get intent client secret
                            var invoiceGetOptions = new InvoiceGetOptions();
                            invoiceGetOptions.AddExpand("payment_intent");
                            invoice = await invoiceService.GetAsync(invoice.Id, invoiceGetOptions);
                            paymentIntentClientSecret = invoice?.PaymentIntent?.ClientSecret;
                        }
                        else
                        {
                            throw new GatewayException("Unable to pay invoice.");
                        }
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
                                Balance = customer.Balance
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
                                Quantity = item.Proration ? 1 : item.Quantity,
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

                    if(e is StripeException strEx &&
                        (strEx.StripeError?.Message?.Contains("cannot be used because it is not verified") ?? false))
                    {
                        throw new GatewayException("Bank account is not yet verified.");
                    }

                    throw e;
                }
            }
            return new Tuple<bool, string>(invoiceNow, paymentIntentClientSecret);
        }

        public async Task CancelSubscriptionAsync(ISubscriber subscriber, bool endOfPeriod = false,
            bool skipInAppPurchaseCheck = false)
        {
            if(subscriber == null)
            {
                throw new ArgumentNullException(nameof(subscriber));
            }

            if(string.IsNullOrWhiteSpace(subscriber.GatewaySubscriptionId))
            {
                throw new GatewayException("No subscription.");
            }

            if(!string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId) && !skipInAppPurchaseCheck)
            {
                var customerService = new CustomerService();
                var customer = await customerService.GetAsync(subscriber.GatewayCustomerId);
                if(customer.Metadata.ContainsKey("appleReceipt"))
                {
                    throw new BadRequestException("You are required to manage your subscription from the app store.");
                }
            }

            var subscriptionService = new SubscriptionService();
            var sub = await subscriptionService.GetAsync(subscriber.GatewaySubscriptionId);
            if(sub == null)
            {
                throw new GatewayException("Subscription was not found.");
            }

            if(sub.CanceledAt.HasValue || sub.Status == "canceled" || sub.Status == "unpaid" ||
                sub.Status == "incomplete_expired")
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

            if((sub.Status != "active" && sub.Status != "trialing" && !sub.Status.StartsWith("incomplete")) ||
                !sub.CanceledAt.HasValue)
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
            string paymentToken, bool allowInAppPurchases = false)
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
            AppleReceiptStatus appleReceiptStatus = null;
            Braintree.Customer braintreeCustomer = null;
            string stipeCustomerSourceToken = null;
            string stipeCustomerPaymentMethodId = null;
            var stripeCustomerMetadata = new Dictionary<string, string>();
            var stripePaymentMethod = paymentMethodType == PaymentMethodType.Card ||
                paymentMethodType == PaymentMethodType.BankAccount;
            var inAppPurchase = paymentMethodType == PaymentMethodType.AppleInApp ||
                paymentMethodType == PaymentMethodType.GoogleInApp;

            var cardService = new CardService();
            var bankSerice = new BankAccountService();
            var customerService = new CustomerService();
            var paymentMethodService = new PaymentMethodService();
            Customer customer = null;

            if(!allowInAppPurchases && inAppPurchase)
            {
                throw new GatewayException("In-app purchase payment method is not allowed.");
            }

            if(!subscriber.IsUser() && inAppPurchase)
            {
                throw new GatewayException("In-app purchase payment method is only allowed for users.");
            }

            if(!string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
            {
                customer = await customerService.GetAsync(subscriber.GatewayCustomerId);
                if(customer.Metadata?.Any() ?? false)
                {
                    stripeCustomerMetadata = customer.Metadata;
                }
            }

            if(inAppPurchase && customer != null && customer.Balance != 0)
            {
                throw new GatewayException("Customer balance cannot exist when using in-app purchases.");
            }

            if(!inAppPurchase && customer != null && stripeCustomerMetadata.ContainsKey("appleReceipt"))
            {
                throw new GatewayException("Cannot change from in-app payment method. Contact support.");
            }

            var hadBtCustomer = stripeCustomerMetadata.ContainsKey("btCustomerId");
            if(stripePaymentMethod)
            {
                if(paymentToken.StartsWith("pm_"))
                {
                    stipeCustomerPaymentMethodId = paymentToken;
                }
                else
                {
                    stipeCustomerSourceToken = paymentToken;
                }
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
            else if(paymentMethodType == PaymentMethodType.AppleInApp)
            {
                appleReceiptStatus = await _appleIapService.GetVerifiedReceiptStatusAsync(paymentToken);
                if(appleReceiptStatus == null)
                {
                    throw new GatewayException("Cannot verify Apple in-app purchase.");
                }
                await VerifyAppleReceiptNotInUseAsync(appleReceiptStatus.GetOriginalTransactionId(), subscriber);
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

            if(appleReceiptStatus != null)
            {
                var originalTransactionId = appleReceiptStatus.GetOriginalTransactionId();
                if(stripeCustomerMetadata.ContainsKey("appleReceipt"))
                {
                    if(originalTransactionId != stripeCustomerMetadata["appleReceipt"])
                    {
                        var nowSec = Utilities.CoreHelpers.ToEpocSeconds(DateTime.UtcNow);
                        stripeCustomerMetadata.Add($"appleReceipt_{nowSec}", stripeCustomerMetadata["appleReceipt"]);
                    }
                    stripeCustomerMetadata["appleReceipt"] = originalTransactionId;
                }
                else
                {
                    stripeCustomerMetadata.Add("appleReceipt", originalTransactionId);
                }
                await _appleIapService.SaveReceiptAsync(appleReceiptStatus, subscriber.Id);
            }

            try
            {
                if(customer == null)
                {
                    customer = await customerService.CreateAsync(new CustomerCreateOptions
                    {
                        Description = subscriber.BillingName(),
                        Email = subscriber.BillingEmailAddress(),
                        Metadata = stripeCustomerMetadata,
                        Source = stipeCustomerSourceToken,
                        PaymentMethodId = stipeCustomerPaymentMethodId,
                        InvoiceSettings = new CustomerInvoiceSettingsOptions
                        {
                            DefaultPaymentMethodId = stipeCustomerPaymentMethodId
                        }
                    });

                    subscriber.Gateway = GatewayType.Stripe;
                    subscriber.GatewayCustomerId = customer.Id;
                    createdCustomer = true;
                }

                if(!createdCustomer)
                {
                    string defaultSourceId = null;
                    string defaultPaymentMethodId = null;
                    if(stripePaymentMethod)
                    {
                        if(!string.IsNullOrWhiteSpace(stipeCustomerSourceToken) && paymentToken.StartsWith("btok_"))
                        {
                            var bankAccount = await bankSerice.CreateAsync(customer.Id, new BankAccountCreateOptions
                            {
                                Source = paymentToken
                            });
                            defaultSourceId = bankAccount.Id;
                        }
                        else if(!string.IsNullOrWhiteSpace(stipeCustomerPaymentMethodId))
                        {
                            await paymentMethodService.AttachAsync(stipeCustomerPaymentMethodId,
                                new PaymentMethodAttachOptions { CustomerId = customer.Id });
                            defaultPaymentMethodId = stipeCustomerPaymentMethodId;
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

                    var cardPaymentMethods = paymentMethodService.ListAutoPaging(new PaymentMethodListOptions
                    {
                        CustomerId = customer.Id,
                        Type = "card"
                    });
                    foreach(var cardMethod in cardPaymentMethods.Where(m => m.Id != defaultPaymentMethodId))
                    {
                        await paymentMethodService.DetachAsync(cardMethod.Id, new PaymentMethodDetachOptions());
                    }

                    customer = await customerService.UpdateAsync(customer.Id, new CustomerUpdateOptions
                    {
                        Metadata = stripeCustomerMetadata,
                        DefaultSource = defaultSourceId,
                        InvoiceSettings = new CustomerInvoiceSettingsOptions
                        {
                            DefaultPaymentMethodId = defaultPaymentMethodId
                        }
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
                Balance = customer.Balance - (long)(creditAmount * 100)
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
            var invoiceService = new InvoiceService();

            if(!string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
            {
                Customer customer = null;
                try
                {
                    var customerOptions = new CustomerGetOptions();
                    customerOptions.AddExpand("default_source");
                    customerOptions.AddExpand("invoice_settings.default_payment_method");
                    customer = await customerService.GetAsync(subscriber.GatewayCustomerId, customerOptions);
                }
                catch(StripeException) { }
                if(customer != null)
                {
                    billingInfo.Balance = customer.Balance / 100M;

                    if(customer.Metadata?.ContainsKey("appleReceipt") ?? false)
                    {
                        billingInfo.PaymentSource = new BillingInfo.BillingSource
                        {
                            Type = PaymentMethodType.AppleInApp
                        };
                    }
                    else if(customer.Metadata?.ContainsKey("btCustomerId") ?? false)
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
                    else if(customer.InvoiceSettings?.DefaultPaymentMethod?.Type == "card")
                    {
                        billingInfo.PaymentSource = new BillingInfo.BillingSource(
                            customer.InvoiceSettings.DefaultPaymentMethod);
                    }
                    else if(customer.DefaultSource != null &&
                        (customer.DefaultSource is Card || customer.DefaultSource is BankAccount))
                    {
                        billingInfo.PaymentSource = new BillingInfo.BillingSource(customer.DefaultSource);
                    }
                    if(billingInfo.PaymentSource == null)
                    {
                        var paymentMethod = GetLatestCardPaymentMethod(customer.Id);
                        if(paymentMethod != null)
                        {
                            billingInfo.PaymentSource = new BillingInfo.BillingSource(paymentMethod);
                        }
                    }

                    var invoices = await invoiceService.ListAsync(new InvoiceListOptions
                    {
                        CustomerId = customer.Id,
                        Limit = 50
                    });
                    billingInfo.Invoices = invoices.Data.Where(i => i.Status != "void" && i.Status != "draft")
                        .OrderByDescending(i => i.Created).Select(i => new BillingInfo.BillingInvoice(i));
                }
            }

            return billingInfo;
        }

        public async Task<SubscriptionInfo> GetSubscriptionAsync(ISubscriber subscriber)
        {
            var subscriptionInfo = new SubscriptionInfo();

            if(subscriber.IsUser() && !string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
            {
                var customerService = new CustomerService();
                var customer = await customerService.GetAsync(subscriber.GatewayCustomerId);
                subscriptionInfo.UsingInAppPurchase = customer.Metadata.ContainsKey("appleReceipt");
            }

            if(!string.IsNullOrWhiteSpace(subscriber.GatewaySubscriptionId))
            {
                var subscriptionService = new SubscriptionService();
                var invoiceService = new InvoiceService();

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

        private PaymentMethod GetLatestCardPaymentMethod(string customerId)
        {
            var paymentMethodService = new PaymentMethodService();
            var cardPaymentMethods = paymentMethodService.ListAutoPaging(
                new PaymentMethodListOptions { CustomerId = customerId, Type = "card" });
            return cardPaymentMethods.OrderByDescending(m => m.Created).FirstOrDefault();
        }

        private async Task VerifyAppleReceiptNotInUseAsync(string receiptOriginalTransactionId, ISubscriber subscriber)
        {
            var existingReceipt = await _appleIapService.GetReceiptAsync(receiptOriginalTransactionId);
            if(existingReceipt != null && existingReceipt.Item2.HasValue && existingReceipt.Item2 != subscriber.Id)
            {
                var existingUser = await _userRepository.GetByIdAsync(existingReceipt.Item2.Value);
                if(existingUser != null)
                {
                    throw new GatewayException("Apple receipt already in use by another user.");
                }
            }
        }
    }
}
