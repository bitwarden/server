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
using StripeTaxRate = Stripe.TaxRate;
using TaxRate = Bit.Core.Models.Table.TaxRate;

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
        private readonly ITaxRateRepository _taxRateRepository;

        public StripePaymentService(
            ITransactionRepository transactionRepository,
            IUserRepository userRepository,
            GlobalSettings globalSettings,
            IAppleIapService appleIapService,
            ILogger<StripePaymentService> logger,
            ITaxRateRepository taxRateRepository)
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
            _taxRateRepository = taxRateRepository;
        }

        public async Task<string> PurchaseOrganizationAsync(Organization org, PaymentMethodType paymentMethodType,
            string paymentToken, Models.StaticStore.Plan plan, short additionalStorageGb,
            short additionalSeats, bool premiumAccessAddon, TaxInfo taxInfo)
        {
            var customerService = new CustomerService();

            Braintree.Customer braintreeCustomer = null;
            string stipeCustomerSourceToken = null;
            string stipeCustomerPaymentMethodId = null;
            var stripeCustomerMetadata = new Dictionary<string, string>();
            var stripePaymentMethod = paymentMethodType == PaymentMethodType.Card ||
                paymentMethodType == PaymentMethodType.BankAccount;

            if (stripePaymentMethod && !string.IsNullOrWhiteSpace(paymentToken))
            {
                if (paymentToken.StartsWith("pm_"))
                {
                    stipeCustomerPaymentMethodId = paymentToken;
                }
                else
                {
                    stipeCustomerSourceToken = paymentToken;
                }
            }
            else if (paymentMethodType == PaymentMethodType.PayPal)
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

                if (!customerResult.IsSuccess() || customerResult.Target.PaymentMethods.Length == 0)
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

            if (taxInfo != null && !string.IsNullOrWhiteSpace(taxInfo.BillingAddressCountry) && !string.IsNullOrWhiteSpace(taxInfo.BillingAddressPostalCode))
            {
                var taxRateSearch = new TaxRate() 
                {
                    Country = taxInfo.BillingAddressCountry,
                    PostalCode = taxInfo.BillingAddressPostalCode
                };
                var taxRates = await _taxRateRepository.GetByLocationAsync(taxRateSearch);

                // should only be one tax rate per country/zip combo
                var taxRate = taxRates.FirstOrDefault();
                if (taxRate != null)
                {
                    taxInfo.StripeTaxRateId = taxRate.Id;
                }
            }

            var subCreateOptions = new OrganizationPurchaseSubscriptionOptions(org, plan, taxInfo, additionalSeats, additionalStorageGb, premiumAccessAddon);

            Customer customer = null;
            Subscription subscription;
            try
            {
                customer = await customerService.CreateAsync(new CustomerCreateOptions
                {
                    Description = org.BusinessName,
                    Email = org.BillingEmail,
                    Source = stipeCustomerSourceToken,
                    PaymentMethod = stipeCustomerPaymentMethodId,
                    Metadata = stripeCustomerMetadata,
                    InvoiceSettings = new CustomerInvoiceSettingsOptions
                    {
                        DefaultPaymentMethod = stipeCustomerPaymentMethodId
                    },
                    Address = new AddressOptions
                    {
                        Country = taxInfo.BillingAddressCountry,
                        PostalCode = taxInfo.BillingAddressPostalCode,
                        // Line1 is required in Stripe's API, suggestion in Docs is to use Business Name intead.
                        Line1 = taxInfo.BillingAddressLine1 ?? string.Empty,
                        Line2 = taxInfo.BillingAddressLine2,
                        City = taxInfo.BillingAddressCity,
                        State = taxInfo.BillingAddressState,
                    },
                    TaxIdData = !taxInfo.HasTaxId ? null : new List<CustomerTaxIdDataOptions>
                    {
                        new CustomerTaxIdDataOptions
                        {
                            Type = taxInfo.TaxIdType,
                            Value = taxInfo.TaxIdNumber,
                        },
                    },
                });
                subCreateOptions.AddExpand("latest_invoice.payment_intent");
                subCreateOptions.Customer = customer.Id;
                var subscriptionService = new SubscriptionService();
                subscription = await subscriptionService.CreateAsync(subCreateOptions);
                if (subscription.Status == "incomplete" && subscription.LatestInvoice?.PaymentIntent != null)
                {
                    if (subscription.LatestInvoice.PaymentIntent.Status == "requires_payment_method")
                    {
                        await subscriptionService.CancelAsync(subscription.Id, new SubscriptionCancelOptions());
                        throw new GatewayException("Payment method was declined.");
                    }
                }
            }
            catch (Exception e)
            {
                if (customer != null)
                {
                    await customerService.DeleteAsync(customer.Id);
                }
                if (braintreeCustomer != null)
                {
                    await _btGateway.Customer.DeleteAsync(braintreeCustomer.Id);
                }
                throw e;
            }

            org.Gateway = GatewayType.Stripe;
            org.GatewayCustomerId = customer.Id;
            org.GatewaySubscriptionId = subscription.Id;

            if (subscription.Status == "incomplete" &&
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
            short additionalStorageGb, short additionalSeats, bool premiumAccessAddon, TaxInfo taxInfo)
        {
            if (!string.IsNullOrWhiteSpace(org.GatewaySubscriptionId))
            {
                throw new BadRequestException("Organization already has a subscription.");
            }

            var customerService = new CustomerService();
            var customerOptions = new CustomerGetOptions();
            customerOptions.AddExpand("default_source");
            customerOptions.AddExpand("invoice_settings.default_payment_method");
            var customer = await customerService.GetAsync(org.GatewayCustomerId, customerOptions);
            if (customer == null)
            {
                throw new GatewayException("Could not find customer payment profile.");
            }

            if (taxInfo != null && !string.IsNullOrWhiteSpace(taxInfo.BillingAddressCountry) && !string.IsNullOrWhiteSpace(taxInfo.BillingAddressPostalCode))
            {
                var taxRateSearch = new TaxRate() 
                {
                    Country = taxInfo.BillingAddressCountry,
                    PostalCode = taxInfo.BillingAddressPostalCode
                };
                var taxRates = await _taxRateRepository.GetByLocationAsync(taxRateSearch);

                // should only be one tax rate per country/zip combo
                var taxRate = taxRates.FirstOrDefault();
                if (taxRate != null)
                {
                    taxInfo.StripeTaxRateId = taxRate.Id;
                }
            }

            var subCreateOptions = new OrganizationUpgradeSubscriptionOptions(customer.Id, org, plan, taxInfo, additionalSeats, additionalStorageGb, premiumAccessAddon);
            var stripePaymentMethod = false;
            var paymentMethodType = PaymentMethodType.Credit;
            var hasBtCustomerId = customer.Metadata.ContainsKey("btCustomerId");
            if (hasBtCustomerId)
            {
                paymentMethodType = PaymentMethodType.PayPal;
            }
            else
            {
                if (customer.InvoiceSettings?.DefaultPaymentMethod?.Type == "card")
                {
                    paymentMethodType = PaymentMethodType.Card;
                    stripePaymentMethod = true;
                }
                else if (customer.DefaultSource != null)
                {
                    if (customer.DefaultSource is Card || customer.DefaultSource is SourceCard)
                    {
                        paymentMethodType = PaymentMethodType.Card;
                        stripePaymentMethod = true;
                    }
                    else if (customer.DefaultSource is BankAccount || customer.DefaultSource is SourceAchDebit)
                    {
                        paymentMethodType = PaymentMethodType.BankAccount;
                        stripePaymentMethod = true;
                    }
                }
                else
                {
                    var paymentMethod = GetLatestCardPaymentMethod(customer.Id);
                    if (paymentMethod != null)
                    {
                        paymentMethodType = PaymentMethodType.Card;
                        stripePaymentMethod = true;
                        subCreateOptions.DefaultPaymentMethod = paymentMethod.Id;
                    }
                }
            }

            var subscription = await ChargeForNewSubscriptionAsync(org, customer, false,
                stripePaymentMethod, paymentMethodType, subCreateOptions, null);
            org.GatewaySubscriptionId = subscription.Id;

            if (subscription.Status == "incomplete" &&
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
            string paymentToken, short additionalStorageGb, TaxInfo taxInfo)
        {
            if (paymentMethodType != PaymentMethodType.Credit && string.IsNullOrWhiteSpace(paymentToken))
            {
                throw new BadRequestException("Payment token is required.");
            }
            if (paymentMethodType == PaymentMethodType.Credit &&
                (user.Gateway != GatewayType.Stripe || string.IsNullOrWhiteSpace(user.GatewayCustomerId)))
            {
                throw new BadRequestException("Your account does not have any credit available.");
            }
            if (paymentMethodType == PaymentMethodType.BankAccount || paymentMethodType == PaymentMethodType.GoogleInApp)
            {
                throw new GatewayException("Payment method is not supported at this time.");
            }
            if ((paymentMethodType == PaymentMethodType.GoogleInApp ||
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
            if (stripePaymentMethod && !string.IsNullOrWhiteSpace(paymentToken))
            {
                if (paymentToken.StartsWith("pm_"))
                {
                    stipeCustomerPaymentMethodId = paymentToken;
                }
                else
                {
                    stipeCustomerSourceToken = paymentToken;
                }
            }

            if (user.Gateway == GatewayType.Stripe && !string.IsNullOrWhiteSpace(user.GatewayCustomerId))
            {
                if (!string.IsNullOrWhiteSpace(paymentToken))
                {
                    try
                    {
                        await UpdatePaymentMethodAsync(user, paymentMethodType, paymentToken, true, taxInfo);
                    }
                    catch (Exception e)
                    {
                        var message = e.Message.ToLowerInvariant();
                        if (message.Contains("apple") || message.Contains("in-app"))
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

            if (customer == null && !string.IsNullOrWhiteSpace(paymentToken))
            {
                var stripeCustomerMetadata = new Dictionary<string, string>();
                if (paymentMethodType == PaymentMethodType.PayPal)
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

                    if (!customerResult.IsSuccess() || customerResult.Target.PaymentMethods.Length == 0)
                    {
                        throw new GatewayException("Failed to create PayPal customer record.");
                    }

                    braintreeCustomer = customerResult.Target;
                    stripeCustomerMetadata.Add("btCustomerId", braintreeCustomer.Id);
                }
                else if (paymentMethodType == PaymentMethodType.AppleInApp)
                {
                    var verifiedReceiptStatus = await _appleIapService.GetVerifiedReceiptStatusAsync(paymentToken);
                    if (verifiedReceiptStatus == null)
                    {
                        throw new GatewayException("Cannot verify apple in-app purchase.");
                    }
                    var receiptOriginalTransactionId = verifiedReceiptStatus.GetOriginalTransactionId();
                    await VerifyAppleReceiptNotInUseAsync(receiptOriginalTransactionId, user);
                    await _appleIapService.SaveReceiptAsync(verifiedReceiptStatus, user.Id);
                    stripeCustomerMetadata.Add("appleReceipt", receiptOriginalTransactionId);
                }
                else if (!stripePaymentMethod)
                {
                    throw new GatewayException("Payment method is not supported at this time.");
                }

                customer = await customerService.CreateAsync(new CustomerCreateOptions
                {
                    Description = user.Name,
                    Email = user.Email,
                    Metadata = stripeCustomerMetadata,
                    PaymentMethod = stipeCustomerPaymentMethodId,
                    Source = stipeCustomerSourceToken,
                    InvoiceSettings = new CustomerInvoiceSettingsOptions
                    {
                        DefaultPaymentMethod = stipeCustomerPaymentMethodId
                    },
                    Address = new AddressOptions
                    {
                        Line1 = string.Empty,
                        Country = taxInfo.BillingAddressCountry,
                        PostalCode = taxInfo.BillingAddressPostalCode,
                    },
                });
                createdStripeCustomer = true;
            }

            if (customer == null)
            {
                throw new GatewayException("Could not set up customer payment profile.");
            }

            var subCreateOptions = new SubscriptionCreateOptions
            {
                Customer = customer.Id,
                Items = new List<SubscriptionItemOptions>(),
                Metadata = new Dictionary<string, string>
                {
                    [user.GatewayIdField()] = user.Id.ToString()
                }
            };

            subCreateOptions.Items.Add(new SubscriptionItemOptions
            {
                Plan = paymentMethodType == PaymentMethodType.AppleInApp ? PremiumPlanAppleIapId : PremiumPlanId,
                Quantity = 1,
            });

            if (string.IsNullOrWhiteSpace(taxInfo?.BillingAddressCountry) 
                    && string.IsNullOrWhiteSpace(taxInfo?.BillingAddressPostalCode)) 
            {
                var taxRates = await _taxRateRepository.GetByLocationAsync(
                    new Bit.Core.Models.Table.TaxRate()
                    {
                        Country = taxInfo.BillingAddressCountry,
                        PostalCode = taxInfo.BillingAddressPostalCode
                    }
                );
                var taxRate = taxRates.FirstOrDefault();
                if (taxRate != null)
                {
                    subCreateOptions.DefaultTaxRates = new List<string>(1) 
                    { 
                        taxRate.Id 
                    };
                }
            }

            if (additionalStorageGb > 0)
            {
                subCreateOptions.Items.Add(new SubscriptionItemOptions
                {
                    Plan = StoragePlanId,
                    Quantity = additionalStorageGb
                });
            }

            var subscription = await ChargeForNewSubscriptionAsync(user, customer, createdStripeCustomer,
                stripePaymentMethod, paymentMethodType, subCreateOptions, braintreeCustomer);

            user.Gateway = GatewayType.Stripe;
            user.GatewayCustomerId = customer.Id;
            user.GatewaySubscriptionId = subscription.Id;

            if (subscription.Status == "incomplete" &&
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
                if (!stripePaymentMethod)
                {
                    var previewInvoice = await invoiceService.UpcomingAsync(new UpcomingInvoiceOptions
                    {
                        Customer = customer.Id,
                        SubscriptionItems = ToInvoiceSubscriptionItemOptions(subCreateOptions.Items)
                    });

                    if (previewInvoice.AmountDue > 0)
                    {
                        var appleReceiptOrigTransactionId = customer.Metadata != null &&
                            customer.Metadata.ContainsKey("appleReceipt") ? customer.Metadata["appleReceipt"] : null;
                        var braintreeCustomerId = customer.Metadata != null &&
                            customer.Metadata.ContainsKey("btCustomerId") ? customer.Metadata["btCustomerId"] : null;
                        if (!string.IsNullOrWhiteSpace(appleReceiptOrigTransactionId))
                        {
                            if (!subcriber.IsUser())
                            {
                                throw new GatewayException("In-app purchase is only allowed for users.");
                            }

                            var appleReceipt = await _appleIapService.GetReceiptAsync(
                                appleReceiptOrigTransactionId);
                            var verifiedAppleReceipt = await _appleIapService.GetVerifiedReceiptStatusAsync(
                                appleReceipt.Item1);
                            if (verifiedAppleReceipt == null)
                            {
                                throw new GatewayException("Failed to get Apple in-app purchase receipt data.");
                            }
                            subInvoiceMetadata.Add("appleReceipt", verifiedAppleReceipt.GetOriginalTransactionId());
                            var lastTransactionId = verifiedAppleReceipt.GetLastTransactionId();
                            subInvoiceMetadata.Add("appleReceiptTransactionId", lastTransactionId);
                            var existingTransaction = await _transactionRepository.GetByGatewayIdAsync(
                                GatewayType.AppStore, lastTransactionId);
                            if (existingTransaction == null)
                            {
                                appleTransaction = verifiedAppleReceipt.BuildTransactionFromLastTransaction(
                                    PremiumPlanAppleIapPrice, subcriber.Id);
                                appleTransaction.Type = TransactionType.Charge;
                                await _transactionRepository.CreateAsync(appleTransaction);
                            }
                        }
                        else if (!string.IsNullOrWhiteSpace(braintreeCustomerId))
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

                            if (!transactionResult.IsSuccess())
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
                else if (paymentMethodType == PaymentMethodType.Credit)
                {
                    var previewInvoice = await invoiceService.UpcomingAsync(new UpcomingInvoiceOptions
                    {
                        Customer = customer.Id,
                        SubscriptionItems = ToInvoiceSubscriptionItemOptions(subCreateOptions.Items)
                    });
                    if (previewInvoice.AmountDue > 0)
                    {
                        throw new GatewayException("Your account does not have enough credit available.");
                    }
                }

                subCreateOptions.OffSession = true;
                subCreateOptions.AddExpand("latest_invoice.payment_intent");
                var subscriptionService = new SubscriptionService();
                subscription = await subscriptionService.CreateAsync(subCreateOptions);
                if (subscription.Status == "incomplete" && subscription.LatestInvoice?.PaymentIntent != null)
                {
                    if (subscription.LatestInvoice.PaymentIntent.Status == "requires_payment_method")
                    {
                        await subscriptionService.CancelAsync(subscription.Id, new SubscriptionCancelOptions());
                        throw new GatewayException("Payment method was declined.");
                    }
                }

                if (!stripePaymentMethod && subInvoiceMetadata.Any())
                {
                    var invoices = await invoiceService.ListAsync(new InvoiceListOptions
                    {
                        Subscription = subscription.Id
                    });

                    var invoice = invoices?.FirstOrDefault();
                    if (invoice == null)
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
            catch (Exception e)
            {
                if (customer != null)
                {
                    if (createdStripeCustomer)
                    {
                        await customerService.DeleteAsync(customer.Id);
                    }
                    else if (addedCreditToStripeCustomer || customer.Balance < 0)
                    {
                        await customerService.UpdateAsync(customer.Id, new CustomerUpdateOptions
                        {
                            Balance = customer.Balance
                        });
                    }
                }
                if (braintreeTransaction != null)
                {
                    await _btGateway.Transaction.RefundAsync(braintreeTransaction.Id);
                }
                if (braintreeCustomer != null)
                {
                    await _btGateway.Customer.DeleteAsync(braintreeCustomer.Id);
                }
                if (appleTransaction != null)
                {
                    await _transactionRepository.DeleteAsync(appleTransaction);
                }

                if (e is StripeException strEx &&
                    (strEx.StripeError?.Message?.Contains("cannot be used because it is not verified") ?? false))
                {
                    throw new GatewayException("Bank account is not yet verified.");
                }

                throw e;
            }
        }

        private List<InvoiceSubscriptionItemOptions> ToInvoiceSubscriptionItemOptions(
            List<SubscriptionItemOptions> subItemOptions)
        {
            return subItemOptions.Select(si => new InvoiceSubscriptionItemOptions
            {
                Plan = si.Plan,
                Quantity = si.Quantity
            }).ToList();
        }

        public async Task<string> AdjustStorageAsync(IStorableSubscriber storableSubscriber, int additionalStorage,
            string storagePlanId)
        {
            var subscriptionService = new SubscriptionService();
            var sub = await subscriptionService.GetAsync(storableSubscriber.GatewaySubscriptionId);
            if (sub == null)
            {
                throw new GatewayException("Subscription not found.");
            }

            var prorationDate = DateTime.UtcNow;
            var storageItem = sub.Items?.FirstOrDefault(i => i.Plan.Id == storagePlanId);
            // Retain original collection method
            var collectionMethod = sub.CollectionMethod;

            var subUpdateOptions = new SubscriptionUpdateOptions
            {
                Items = new List<SubscriptionItemOptions>
                {
                    new SubscriptionItemOptions
                    {
                        Id = storageItem?.Id,
                        Plan = storagePlanId,
                        Quantity = additionalStorage,
                        Deleted = (storageItem?.Id != null && additionalStorage == 0) ? true : (bool?)null
                    }
                },
                ProrationBehavior = "always_invoice",
                DaysUntilDue = 1,
                CollectionMethod = "send_invoice",
                ProrationDate = prorationDate,
            };

            var customer = await new CustomerService().GetAsync(sub.CustomerId);
            if (!string.IsNullOrWhiteSpace(customer?.Address?.Country) 
                    && !string.IsNullOrWhiteSpace(customer?.Address?.PostalCode))
            {
                var taxRates = await _taxRateRepository.GetByLocationAsync(
                    new Bit.Core.Models.Table.TaxRate()
                    {
                        Country = customer.Address.Country,
                        PostalCode = customer.Address.PostalCode
                    }
                );
                var taxRate = taxRates.FirstOrDefault();
                if (taxRate != null && !sub.DefaultTaxRates.Any(x => x.Equals(taxRate.Id)))
                {
                    subUpdateOptions.DefaultTaxRates = new List<string>(1) 
                    { 
                        taxRate.Id 
                    };
                }
            }

            var subResponse = await subscriptionService.UpdateAsync(sub.Id, subUpdateOptions);

            string paymentIntentClientSecret = null;
            if (additionalStorage > 0)
            {
                try
                {
                    paymentIntentClientSecret = await PayInvoiceAfterSubscriptionChangeAsync(
                        storableSubscriber, subResponse?.LatestInvoiceId);
                }
                catch
                {
                    // Need to revert the subscription
                    await subscriptionService.UpdateAsync(sub.Id, new SubscriptionUpdateOptions
                    {
                        Items = new List<SubscriptionItemOptions>
                        {
                            new SubscriptionItemOptions
                            {
                                Id = storageItem?.Id,
                                Plan = storagePlanId,
                                Quantity = storageItem?.Quantity ?? 0,
                                Deleted = (storageItem?.Id == null || (storageItem?.Quantity ?? 0) == 0)
                                    ? true : (bool?)null
                            }
                        },
                        // This proration behavior prevents a false "credit" from
                        //  being applied forward to the next month's invoice
                        ProrationBehavior = "none",
                        CollectionMethod = collectionMethod,
                    });
                    throw;
                }
            }

            // Change back the subscription collection method
            if (collectionMethod != "send_invoice")
            {
                await subscriptionService.UpdateAsync(sub.Id, new SubscriptionUpdateOptions
                {
                    CollectionMethod = collectionMethod,
                });
            }

            return paymentIntentClientSecret;
        }

        public async Task CancelAndRecoverChargesAsync(ISubscriber subscriber)
        {
            if (!string.IsNullOrWhiteSpace(subscriber.GatewaySubscriptionId))
            {
                var subscriptionService = new SubscriptionService();
                await subscriptionService.CancelAsync(subscriber.GatewaySubscriptionId,
                    new SubscriptionCancelOptions());
            }

            if (string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
            {
                return;
            }

            var customerService = new CustomerService();
            var customer = await customerService.GetAsync(subscriber.GatewayCustomerId);
            if (customer == null)
            {
                return;
            }

            if (customer.Metadata.ContainsKey("btCustomerId"))
            {
                var transactionRequest = new Braintree.TransactionSearchRequest()
                    .CustomerId.Is(customer.Metadata["btCustomerId"]);
                var transactions = _btGateway.Transaction.Search(transactionRequest);

                if ((transactions?.MaximumCount ?? 0) > 0)
                {
                    var txs = transactions.Cast<Braintree.Transaction>().Where(c => c.RefundedTransactionId == null);
                    foreach (var transaction in txs)
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
                    Customer = subscriber.GatewayCustomerId
                });

                if (charges?.Data != null)
                {
                    var refundService = new RefundService();
                    foreach (var charge in charges.Data.Where(c => c.Captured && !c.Refunded))
                    {
                        await refundService.CreateAsync(new RefundCreateOptions { Charge = charge.Id });
                    }
                }
            }

            await customerService.DeleteAsync(subscriber.GatewayCustomerId);
        }

        public async Task<string> PayInvoiceAfterSubscriptionChangeAsync(ISubscriber subscriber, string invoiceId)
        {
            var customerService = new CustomerService();
            var customerOptions = new CustomerGetOptions();
            customerOptions.AddExpand("default_source");
            customerOptions.AddExpand("invoice_settings.default_payment_method");
            var customer = await customerService.GetAsync(subscriber.GatewayCustomerId, customerOptions);
            var usingInAppPaymentMethod = customer.Metadata.ContainsKey("appleReceipt");
            if (usingInAppPaymentMethod)
            {
                throw new BadRequestException("Cannot perform this action with in-app purchase payment method. " +
                    "Contact support.");
            }

            var invoiceService = new InvoiceService();
            string paymentIntentClientSecret = null;

            var invoice = await invoiceService.GetAsync(invoiceId, new InvoiceGetOptions());
            if (invoice == null)
            {
                throw new BadRequestException("Unable to locate draft invoice for subscription update.");
            }

            // Invoice them and pay now instead of waiting until Stripe does this automatically.

            string cardPaymentMethodId = null;
            if (invoice?.AmountDue > 0 && !customer.Metadata.ContainsKey("btCustomerId"))
            {
                var hasDefaultCardPaymentMethod = customer.InvoiceSettings?.DefaultPaymentMethod?.Type == "card";
                var hasDefaultValidSource = customer.DefaultSource != null &&
                    (customer.DefaultSource is Card || customer.DefaultSource is BankAccount);
                if (!hasDefaultCardPaymentMethod && !hasDefaultValidSource)
                {
                    cardPaymentMethodId = GetLatestCardPaymentMethod(customer.Id)?.Id;
                    if (cardPaymentMethodId == null)
                    {
                        // We're going to delete this draft invoice, it can't be paid
                        await invoiceService.DeleteAsync(invoice.Id);
                        throw new BadRequestException("No payment method is available.");
                    }
                }
            }

            Braintree.Transaction braintreeTransaction = null;
            try
            {
                // Finalize the invoice (from Draft) w/o auto-advance so we
                //  can attempt payment manually.
                invoice = await invoiceService.FinalizeInvoiceAsync(invoice.Id, new InvoiceFinalizeOptions
                {
                    AutoAdvance = false,
                });
                var invoicePayOptions = new InvoicePayOptions
                {
                    PaymentMethod = cardPaymentMethodId,
                };
                if (invoice.AmountDue > 0)
                {
                    if (customer?.Metadata?.ContainsKey("btCustomerId") ?? false)
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

                        if (!transactionResult.IsSuccess())
                        {
                            throw new GatewayException("Failed to charge PayPal customer.");
                        }

                        braintreeTransaction = transactionResult.Target;
                        invoice = await invoiceService.UpdateAsync(invoice.Id, new InvoiceUpdateOptions
                        {
                            Metadata = new Dictionary<string, string>
                            {
                                ["btTransactionId"] = braintreeTransaction.Id,
                                ["btPayPalTransactionId"] =
                                    braintreeTransaction.PayPalDetails.AuthorizationId
                            },
                        });
                        invoicePayOptions.PaidOutOfBand = true;
                    }
                }

                try
                {
                    invoice = await invoiceService.PayAsync(invoice.Id, invoicePayOptions);
                }
                catch (StripeException e)
                {
                    if (e.HttpStatusCode == System.Net.HttpStatusCode.PaymentRequired &&
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
            catch (Exception e)
            {
                if (braintreeTransaction != null)
                {
                    await _btGateway.Transaction.RefundAsync(braintreeTransaction.Id);
                }
                if (invoice != null)
                {
                    if (invoice.Status == "paid")
                    {
                        // It's apparently paid, so we need to return w/o throwing an exception
                        return paymentIntentClientSecret;
                    }

                    invoice = await invoiceService.VoidInvoiceAsync(invoice.Id, new InvoiceVoidOptions());

                    // HACK: Workaround for customer balance credit
                    if (invoice.StartingBalance < 0)
                    {
                        // Customer had a balance applied to this invoice. Since we can't fully trust Stripe to
                        //  credit it back to the customer (even though their docs claim they will), we need to
                        //  check that balance against the current customer balance and determine if it needs to be re-applied
                        customer = await customerService.GetAsync(subscriber.GatewayCustomerId, customerOptions);

                        // Assumption: Customer balance should now be $0, otherwise payment would not have failed.
                        if (customer.Balance == 0)
                        {
                            await customerService.UpdateAsync(customer.Id, new CustomerUpdateOptions
                            {
                                Balance = invoice.StartingBalance
                            });
                        }
                    }
                }

                if (e is StripeException strEx &&
                    (strEx.StripeError?.Message?.Contains("cannot be used because it is not verified") ?? false))
                {
                    throw new GatewayException("Bank account is not yet verified.");
                }

                // Let the caller perform any subscription change cleanup
                throw;
            }
            return paymentIntentClientSecret;
        }

        public async Task CancelSubscriptionAsync(ISubscriber subscriber, bool endOfPeriod = false,
            bool skipInAppPurchaseCheck = false)
        {
            if (subscriber == null)
            {
                throw new ArgumentNullException(nameof(subscriber));
            }

            if (string.IsNullOrWhiteSpace(subscriber.GatewaySubscriptionId))
            {
                throw new GatewayException("No subscription.");
            }

            if (!string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId) && !skipInAppPurchaseCheck)
            {
                var customerService = new CustomerService();
                var customer = await customerService.GetAsync(subscriber.GatewayCustomerId);
                if (customer.Metadata.ContainsKey("appleReceipt"))
                {
                    throw new BadRequestException("You are required to manage your subscription from the app store.");
                }
            }

            var subscriptionService = new SubscriptionService();
            var sub = await subscriptionService.GetAsync(subscriber.GatewaySubscriptionId);
            if (sub == null)
            {
                throw new GatewayException("Subscription was not found.");
            }

            if (sub.CanceledAt.HasValue || sub.Status == "canceled" || sub.Status == "unpaid" ||
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
                if (!canceledSub.CanceledAt.HasValue)
                {
                    throw new GatewayException("Unable to cancel subscription.");
                }
            }
            catch (StripeException e)
            {
                if (e.Message != $"No such subscription: {subscriber.GatewaySubscriptionId}")
                {
                    throw e;
                }
            }
        }

        public async Task ReinstateSubscriptionAsync(ISubscriber subscriber)
        {
            if (subscriber == null)
            {
                throw new ArgumentNullException(nameof(subscriber));
            }

            if (string.IsNullOrWhiteSpace(subscriber.GatewaySubscriptionId))
            {
                throw new GatewayException("No subscription.");
            }

            var subscriptionService = new SubscriptionService();
            var sub = await subscriptionService.GetAsync(subscriber.GatewaySubscriptionId);
            if (sub == null)
            {
                throw new GatewayException("Subscription was not found.");
            }

            if ((sub.Status != "active" && sub.Status != "trialing" && !sub.Status.StartsWith("incomplete")) ||
                !sub.CanceledAt.HasValue)
            {
                throw new GatewayException("Subscription is not marked for cancellation.");
            }

            var updatedSub = await subscriptionService.UpdateAsync(sub.Id,
                new SubscriptionUpdateOptions { CancelAtPeriodEnd = false });
            if (updatedSub.CanceledAt.HasValue)
            {
                throw new GatewayException("Unable to reinstate subscription.");
            }
        }

        public async Task<bool> UpdatePaymentMethodAsync(ISubscriber subscriber, PaymentMethodType paymentMethodType,
            string paymentToken, bool allowInAppPurchases = false, TaxInfo taxInfo = null)
        {
            if (subscriber == null)
            {
                throw new ArgumentNullException(nameof(subscriber));
            }

            if (subscriber.Gateway.HasValue && subscriber.Gateway.Value != GatewayType.Stripe)
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

            if (!allowInAppPurchases && inAppPurchase)
            {
                throw new GatewayException("In-app purchase payment method is not allowed.");
            }

            if (!subscriber.IsUser() && inAppPurchase)
            {
                throw new GatewayException("In-app purchase payment method is only allowed for users.");
            }

            if (!string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
            {
                customer = await customerService.GetAsync(subscriber.GatewayCustomerId);
                if (customer.Metadata?.Any() ?? false)
                {
                    stripeCustomerMetadata = customer.Metadata;
                }
            }

            if (inAppPurchase && customer != null && customer.Balance != 0)
            {
                throw new GatewayException("Customer balance cannot exist when using in-app purchases.");
            }

            if (!inAppPurchase && customer != null && stripeCustomerMetadata.ContainsKey("appleReceipt"))
            {
                throw new GatewayException("Cannot change from in-app payment method. Contact support.");
            }

            var hadBtCustomer = stripeCustomerMetadata.ContainsKey("btCustomerId");
            if (stripePaymentMethod)
            {
                if (paymentToken.StartsWith("pm_"))
                {
                    stipeCustomerPaymentMethodId = paymentToken;
                }
                else
                {
                    stipeCustomerSourceToken = paymentToken;
                }
            }
            else if (paymentMethodType == PaymentMethodType.PayPal)
            {
                if (hadBtCustomer)
                {
                    var pmResult = await _btGateway.PaymentMethod.CreateAsync(new Braintree.PaymentMethodRequest
                    {
                        CustomerId = stripeCustomerMetadata["btCustomerId"],
                        PaymentMethodNonce = paymentToken
                    });

                    if (pmResult.IsSuccess())
                    {
                        var customerResult = await _btGateway.Customer.UpdateAsync(
                            stripeCustomerMetadata["btCustomerId"], new Braintree.CustomerRequest
                            {
                                DefaultPaymentMethodToken = pmResult.Target.Token
                            });

                        if (customerResult.IsSuccess() && customerResult.Target.PaymentMethods.Length > 0)
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

                if (!hadBtCustomer)
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

                    if (!customerResult.IsSuccess() || customerResult.Target.PaymentMethods.Length == 0)
                    {
                        throw new GatewayException("Failed to create PayPal customer record.");
                    }

                    braintreeCustomer = customerResult.Target;
                }
            }
            else if (paymentMethodType == PaymentMethodType.AppleInApp)
            {
                appleReceiptStatus = await _appleIapService.GetVerifiedReceiptStatusAsync(paymentToken);
                if (appleReceiptStatus == null)
                {
                    throw new GatewayException("Cannot verify Apple in-app purchase.");
                }
                await VerifyAppleReceiptNotInUseAsync(appleReceiptStatus.GetOriginalTransactionId(), subscriber);
            }
            else
            {
                throw new GatewayException("Payment method is not supported at this time.");
            }

            if (stripeCustomerMetadata.ContainsKey("btCustomerId"))
            {
                if (braintreeCustomer?.Id != stripeCustomerMetadata["btCustomerId"])
                {
                    var nowSec = Utilities.CoreHelpers.ToEpocSeconds(DateTime.UtcNow);
                    stripeCustomerMetadata.Add($"btCustomerId_{nowSec}", stripeCustomerMetadata["btCustomerId"]);
                }
                stripeCustomerMetadata["btCustomerId"] = braintreeCustomer?.Id;
            }
            else if (!string.IsNullOrWhiteSpace(braintreeCustomer?.Id))
            {
                stripeCustomerMetadata.Add("btCustomerId", braintreeCustomer.Id);
            }

            if (appleReceiptStatus != null)
            {
                var originalTransactionId = appleReceiptStatus.GetOriginalTransactionId();
                if (stripeCustomerMetadata.ContainsKey("appleReceipt"))
                {
                    if (originalTransactionId != stripeCustomerMetadata["appleReceipt"])
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
                if (customer == null)
                {
                    customer = await customerService.CreateAsync(new CustomerCreateOptions
                    {
                        Description = subscriber.BillingName(),
                        Email = subscriber.BillingEmailAddress(),
                        Metadata = stripeCustomerMetadata,
                        Source = stipeCustomerSourceToken,
                        PaymentMethod = stipeCustomerPaymentMethodId,
                        InvoiceSettings = new CustomerInvoiceSettingsOptions
                        {
                            DefaultPaymentMethod = stipeCustomerPaymentMethodId
                        },
                        Address = taxInfo == null ? null : new AddressOptions
                        {
                            Country = taxInfo.BillingAddressCountry,
                            PostalCode = taxInfo.BillingAddressPostalCode,
                            Line1 = taxInfo.BillingAddressLine1 ?? string.Empty,
                            Line2 = taxInfo.BillingAddressLine2,
                            City = taxInfo.BillingAddressCity,
                            State = taxInfo.BillingAddressState,
                        },
                    });

                    subscriber.Gateway = GatewayType.Stripe;
                    subscriber.GatewayCustomerId = customer.Id;
                    createdCustomer = true;
                }

                if (!createdCustomer)
                {
                    string defaultSourceId = null;
                    string defaultPaymentMethodId = null;
                    if (stripePaymentMethod)
                    {
                        if (!string.IsNullOrWhiteSpace(stipeCustomerSourceToken) && paymentToken.StartsWith("btok_"))
                        {
                            var bankAccount = await bankSerice.CreateAsync(customer.Id, new BankAccountCreateOptions
                            {
                                Source = paymentToken
                            });
                            defaultSourceId = bankAccount.Id;
                        }
                        else if (!string.IsNullOrWhiteSpace(stipeCustomerPaymentMethodId))
                        {
                            await paymentMethodService.AttachAsync(stipeCustomerPaymentMethodId,
                                new PaymentMethodAttachOptions { Customer = customer.Id });
                            defaultPaymentMethodId = stipeCustomerPaymentMethodId;
                        }
                    }

                    foreach (var source in customer.Sources.Where(s => s.Id != defaultSourceId))
                    {
                        if (source is BankAccount)
                        {
                            await bankSerice.DeleteAsync(customer.Id, source.Id);
                        }
                        else if (source is Card)
                        {
                            await cardService.DeleteAsync(customer.Id, source.Id);
                        }
                    }

                    var cardPaymentMethods = paymentMethodService.ListAutoPaging(new PaymentMethodListOptions
                    {
                        Customer = customer.Id,
                        Type = "card"
                    });
                    foreach (var cardMethod in cardPaymentMethods.Where(m => m.Id != defaultPaymentMethodId))
                    {
                        await paymentMethodService.DetachAsync(cardMethod.Id, new PaymentMethodDetachOptions());
                    }

                    customer = await customerService.UpdateAsync(customer.Id, new CustomerUpdateOptions
                    {
                        Metadata = stripeCustomerMetadata,
                        DefaultSource = defaultSourceId,
                        InvoiceSettings = new CustomerInvoiceSettingsOptions
                        {
                            DefaultPaymentMethod = defaultPaymentMethodId
                        },
                        Address = taxInfo == null ? null : new AddressOptions
                        {
                            Country = taxInfo.BillingAddressCountry,
                            PostalCode = taxInfo.BillingAddressPostalCode,
                            Line1 = taxInfo.BillingAddressLine1 ?? string.Empty,
                            Line2 = taxInfo.BillingAddressLine2,
                            City = taxInfo.BillingAddressCity,
                            State = taxInfo.BillingAddressState,
                        },
                    });
                }
            }
            catch (Exception e)
            {
                if (braintreeCustomer != null && !hadBtCustomer)
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
            if (customerExists)
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
            if (subscriber is User)
            {
                transactions = await _transactionRepository.GetManyByUserIdAsync(subscriber.Id);
            }
            else if (subscriber is Organization)
            {
                transactions = await _transactionRepository.GetManyByOrganizationIdAsync(subscriber.Id);
            }
            if (transactions != null)
            {
                billingInfo.Transactions = transactions?.OrderByDescending(i => i.CreationDate)
                    .Select(t => new BillingInfo.BillingTransaction(t));
            }

            var customerService = new CustomerService();
            var invoiceService = new InvoiceService();

            if (!string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
            {
                Customer customer = null;
                try
                {
                    var customerOptions = new CustomerGetOptions();
                    customerOptions.AddExpand("default_source");
                    customerOptions.AddExpand("invoice_settings.default_payment_method");
                    customer = await customerService.GetAsync(subscriber.GatewayCustomerId, customerOptions);
                }
                catch (StripeException) { }
                if (customer != null)
                {
                    billingInfo.Balance = customer.Balance / 100M;

                    if (customer.Metadata?.ContainsKey("appleReceipt") ?? false)
                    {
                        billingInfo.PaymentSource = new BillingInfo.BillingSource
                        {
                            Type = PaymentMethodType.AppleInApp
                        };
                    }
                    else if (customer.Metadata?.ContainsKey("btCustomerId") ?? false)
                    {
                        try
                        {
                            var braintreeCustomer = await _btGateway.Customer.FindAsync(
                                customer.Metadata["btCustomerId"]);
                            if (braintreeCustomer?.DefaultPaymentMethod != null)
                            {
                                billingInfo.PaymentSource = new BillingInfo.BillingSource(
                                    braintreeCustomer.DefaultPaymentMethod);
                            }
                        }
                        catch (Braintree.Exceptions.NotFoundException) { }
                    }
                    else if (customer.InvoiceSettings?.DefaultPaymentMethod?.Type == "card")
                    {
                        billingInfo.PaymentSource = new BillingInfo.BillingSource(
                            customer.InvoiceSettings.DefaultPaymentMethod);
                    }
                    else if (customer.DefaultSource != null &&
                        (customer.DefaultSource is Card || customer.DefaultSource is BankAccount))
                    {
                        billingInfo.PaymentSource = new BillingInfo.BillingSource(customer.DefaultSource);
                    }
                    if (billingInfo.PaymentSource == null)
                    {
                        var paymentMethod = GetLatestCardPaymentMethod(customer.Id);
                        if (paymentMethod != null)
                        {
                            billingInfo.PaymentSource = new BillingInfo.BillingSource(paymentMethod);
                        }
                    }

                    var invoices = await invoiceService.ListAsync(new InvoiceListOptions
                    {
                        Customer = customer.Id,
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

            if (subscriber.IsUser() && !string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
            {
                var customerService = new CustomerService();
                var customer = await customerService.GetAsync(subscriber.GatewayCustomerId);
                subscriptionInfo.UsingInAppPurchase = customer.Metadata.ContainsKey("appleReceipt");
            }

            if (!string.IsNullOrWhiteSpace(subscriber.GatewaySubscriptionId))
            {
                var subscriptionService = new SubscriptionService();
                var invoiceService = new InvoiceService();

                var sub = await subscriptionService.GetAsync(subscriber.GatewaySubscriptionId);
                if (sub != null)
                {
                    subscriptionInfo.Subscription = new SubscriptionInfo.BillingSubscription(sub);
                }

                if (!sub.CanceledAt.HasValue && !string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
                {
                    try
                    {
                        var upcomingInvoice = await invoiceService.UpcomingAsync(
                            new UpcomingInvoiceOptions { Customer = subscriber.GatewayCustomerId });
                        if (upcomingInvoice != null)
                        {
                            subscriptionInfo.UpcomingInvoice =
                                new SubscriptionInfo.BillingUpcomingInvoice(upcomingInvoice);
                        }
                    }
                    catch (StripeException) { }
                }
            }

            return subscriptionInfo;
        }

        public async Task<TaxInfo> GetTaxInfoAsync(ISubscriber subscriber)
        {
            if (subscriber == null || string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
            {
                return null;
            }

            var customerService = new CustomerService();
            var customer = await customerService.GetAsync(subscriber.GatewayCustomerId);

            if (customer == null)
            {
                return null;
            }

            var address = customer.Address;
            var taxId = customer.TaxIds?.FirstOrDefault();

            // Line1 is required, so if missing we're using the subscriber name
            // see: https://stripe.com/docs/api/customers/create#create_customer-address-line1
            if (address != null && string.IsNullOrWhiteSpace(address.Line1))
            {
                address.Line1 = null;
            }

            return new TaxInfo
            {
                TaxIdNumber = taxId?.Value,
                BillingAddressLine1 = address?.Line1,
                BillingAddressLine2 = address?.Line2,
                BillingAddressCity = address?.City,
                BillingAddressState = address?.State,
                BillingAddressPostalCode = address?.PostalCode,
                BillingAddressCountry = address?.Country,
            };
        }

        public async Task SaveTaxInfoAsync(ISubscriber subscriber, TaxInfo taxInfo)
        {
            if (subscriber != null && !string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
            {
                var customerService = new CustomerService();
                var customer = await customerService.UpdateAsync(subscriber.GatewayCustomerId, new CustomerUpdateOptions
                {
                    Address = new AddressOptions
                    {
                        Line1 = taxInfo.BillingAddressLine1 ?? string.Empty,
                        Line2 = taxInfo.BillingAddressLine2,
                        City = taxInfo.BillingAddressCity,
                        State = taxInfo.BillingAddressState,
                        PostalCode = taxInfo.BillingAddressPostalCode,
                        Country = taxInfo.BillingAddressCountry,
                    },
                });

                if (!subscriber.IsUser() && customer != null)
                {
                    var taxIdService = new TaxIdService();
                    var taxId = customer.TaxIds?.FirstOrDefault();

                    if (taxId != null)
                    {
                        await taxIdService.DeleteAsync(customer.Id, taxId.Id);
                    }
                    if (!string.IsNullOrWhiteSpace(taxInfo.TaxIdNumber) &&
                        !string.IsNullOrWhiteSpace(taxInfo.TaxIdType))
                    {
                        await taxIdService.CreateAsync(customer.Id, new TaxIdCreateOptions
                        {
                            Type = taxInfo.TaxIdType,
                            Value = taxInfo.TaxIdNumber,
                        });
                    }
                }
            }
        }

        public async Task<TaxRate> CreateTaxRateAsync(TaxRate taxRate)
        {
            var stripeTaxRateOptions = new TaxRateCreateOptions()
            {
                DisplayName = $"{taxRate.Country} - {taxRate.PostalCode}",
                Inclusive = false,
                Percentage = taxRate.Rate,
                Active = true
            };
            var taxRateService = new TaxRateService();
            var stripeTaxRate = taxRateService.Create(stripeTaxRateOptions);
            taxRate.Id = stripeTaxRate.Id;
            await _taxRateRepository.CreateAsync(taxRate);
            return taxRate;
        }

        public async Task UpdateTaxRateAsync(TaxRate taxRate)
        {
            if (string.IsNullOrWhiteSpace(taxRate.Id))
            {
                return;
            }

            await ArchiveTaxRateAsync(taxRate);
            await CreateTaxRateAsync(taxRate);
        }

        public async Task ArchiveTaxRateAsync(TaxRate taxRate)
        {
            if (string.IsNullOrWhiteSpace(taxRate.Id))
            {
                return;
            }
            
            var stripeTaxRateService = new TaxRateService();
            var updatedStripeTaxRate = await stripeTaxRateService.UpdateAsync(
                    taxRate.Id, 
                    new TaxRateUpdateOptions() { Active = false }
            );
            if (!updatedStripeTaxRate.Active)
            {
                taxRate.Active = false;
                await _taxRateRepository.ArchiveAsync(taxRate);
            }
        }

        private PaymentMethod GetLatestCardPaymentMethod(string customerId)
        {
            var paymentMethodService = new PaymentMethodService();
            var cardPaymentMethods = paymentMethodService.ListAutoPaging(
                new PaymentMethodListOptions { Customer = customerId, Type = "card" });
            return cardPaymentMethods.OrderByDescending(m => m.Created).FirstOrDefault();
        }

        private async Task VerifyAppleReceiptNotInUseAsync(string receiptOriginalTransactionId, ISubscriber subscriber)
        {
            var existingReceipt = await _appleIapService.GetReceiptAsync(receiptOriginalTransactionId);
            if (existingReceipt != null && existingReceipt.Item2.HasValue && existingReceipt.Item2 != subscriber.Id)
            {
                var existingUser = await _userRepository.GetByIdAsync(existingReceipt.Item2.Value);
                if (existingUser != null)
                {
                    throw new GatewayException("Apple receipt already in use by another user.");
                }
            }
        }
    }
}
