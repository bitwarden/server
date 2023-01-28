using Bit.Billing.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;
using StaticStore = Bit.Core.Models.StaticStore;
using TaxRate = Bit.Core.Entities.TaxRate;

namespace Bit.Core.Services;

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
    private readonly Braintree.IBraintreeGateway _btGateway;
    private readonly ITaxRateRepository _taxRateRepository;
    private readonly IStripeAdapter _stripeAdapter;

    public StripePaymentService(
        ITransactionRepository transactionRepository,
        IUserRepository userRepository,
        IAppleIapService appleIapService,
        ILogger<StripePaymentService> logger,
        ITaxRateRepository taxRateRepository,
        IStripeAdapter stripeAdapter,
        Braintree.IBraintreeGateway braintreeGateway)
    {
        _transactionRepository = transactionRepository;
        _userRepository = userRepository;
        _appleIapService = appleIapService;
        _logger = logger;
        _taxRateRepository = taxRateRepository;
        _stripeAdapter = stripeAdapter;
        _btGateway = braintreeGateway;
    }

    public async Task<string> PurchaseOrganizationAsync(Organization org, PaymentMethodType paymentMethodType,
        string paymentToken, StaticStore.Plan plan, short additionalStorageGb,
        int additionalSeats, bool premiumAccessAddon, TaxInfo taxInfo)
    {
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
            var taxRateSearch = new TaxRate
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

        Stripe.Customer customer = null;
        Stripe.Subscription subscription;
        try
        {
            customer = await _stripeAdapter.CustomerCreateAsync(new Stripe.CustomerCreateOptions
            {
                Description = org.BusinessName,
                Email = org.BillingEmail,
                Source = stipeCustomerSourceToken,
                PaymentMethod = stipeCustomerPaymentMethodId,
                Metadata = stripeCustomerMetadata,
                InvoiceSettings = new Stripe.CustomerInvoiceSettingsOptions
                {
                    DefaultPaymentMethod = stipeCustomerPaymentMethodId
                },
                Address = new Stripe.AddressOptions
                {
                    Country = taxInfo.BillingAddressCountry,
                    PostalCode = taxInfo.BillingAddressPostalCode,
                    // Line1 is required in Stripe's API, suggestion in Docs is to use Business Name intead.
                    Line1 = taxInfo.BillingAddressLine1 ?? string.Empty,
                    Line2 = taxInfo.BillingAddressLine2,
                    City = taxInfo.BillingAddressCity,
                    State = taxInfo.BillingAddressState,
                },
                TaxIdData = !taxInfo.HasTaxId ? null : new List<Stripe.CustomerTaxIdDataOptions>
                {
                    new Stripe.CustomerTaxIdDataOptions
                    {
                        Type = taxInfo.TaxIdType,
                        Value = taxInfo.TaxIdNumber,
                    },
                },
            });
            subCreateOptions.AddExpand("latest_invoice.payment_intent");
            subCreateOptions.Customer = customer.Id;
            subscription = await _stripeAdapter.SubscriptionCreateAsync(subCreateOptions);
            if (subscription.Status == "incomplete" && subscription.LatestInvoice?.PaymentIntent != null)
            {
                if (subscription.LatestInvoice.PaymentIntent.Status == "requires_payment_method")
                {
                    await _stripeAdapter.SubscriptionCancelAsync(subscription.Id, new Stripe.SubscriptionCancelOptions());
                    throw new GatewayException("Payment method was declined.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating customer, walking back operation.");
            if (customer != null)
            {
                await _stripeAdapter.CustomerDeleteAsync(customer.Id);
            }
            if (braintreeCustomer != null)
            {
                await _btGateway.Customer.DeleteAsync(braintreeCustomer.Id);
            }
            throw;
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

    private async Task ChangeOrganizationSponsorship(Organization org, OrganizationSponsorship sponsorship, bool applySponsorship)
    {
        var existingPlan = Utilities.StaticStore.GetPlan(org.PlanType);
        var sponsoredPlan = sponsorship != null ?
            Utilities.StaticStore.GetSponsoredPlan(sponsorship.PlanSponsorshipType.Value) :
            null;
        var subscriptionUpdate = new SponsorOrganizationSubscriptionUpdate(existingPlan, sponsoredPlan, applySponsorship);

        await FinalizeSubscriptionChangeAsync(org, subscriptionUpdate, DateTime.UtcNow);

        var sub = await _stripeAdapter.SubscriptionGetAsync(org.GatewaySubscriptionId);
        org.ExpirationDate = sub.CurrentPeriodEnd;
        sponsorship.ValidUntil = sub.CurrentPeriodEnd;

    }

    public Task SponsorOrganizationAsync(Organization org, OrganizationSponsorship sponsorship) =>
        ChangeOrganizationSponsorship(org, sponsorship, true);

    public Task RemoveOrganizationSponsorshipAsync(Organization org, OrganizationSponsorship sponsorship) =>
        ChangeOrganizationSponsorship(org, sponsorship, false);

    public async Task<string> UpgradeFreeOrganizationAsync(Organization org, StaticStore.Plan plan,
        short additionalStorageGb, int additionalSeats, bool premiumAccessAddon, TaxInfo taxInfo)
    {
        if (!string.IsNullOrWhiteSpace(org.GatewaySubscriptionId))
        {
            throw new BadRequestException("Organization already has a subscription.");
        }

        var customerOptions = new Stripe.CustomerGetOptions();
        customerOptions.AddExpand("default_source");
        customerOptions.AddExpand("invoice_settings.default_payment_method");
        var customer = await _stripeAdapter.CustomerGetAsync(org.GatewayCustomerId, customerOptions);
        if (customer == null)
        {
            throw new GatewayException("Could not find customer payment profile.");
        }

        if (taxInfo != null && !string.IsNullOrWhiteSpace(taxInfo.BillingAddressCountry) && !string.IsNullOrWhiteSpace(taxInfo.BillingAddressPostalCode))
        {
            var taxRateSearch = new TaxRate
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
        var (stripePaymentMethod, paymentMethodType) = IdentifyPaymentMethod(customer, subCreateOptions);

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

    private (bool stripePaymentMethod, PaymentMethodType PaymentMethodType) IdentifyPaymentMethod(
            Stripe.Customer customer, Stripe.SubscriptionCreateOptions subCreateOptions)
    {
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
                if (customer.DefaultSource is Stripe.Card || customer.DefaultSource is Stripe.SourceCard)
                {
                    paymentMethodType = PaymentMethodType.Card;
                    stripePaymentMethod = true;
                }
                else if (customer.DefaultSource is Stripe.BankAccount || customer.DefaultSource is Stripe.SourceAchDebit)
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
        return (stripePaymentMethod, paymentMethodType);
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

        var createdStripeCustomer = false;
        Stripe.Customer customer = null;
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
                        throw;
                    }
                }
            }
            try
            {
                customer = await _stripeAdapter.CustomerGetAsync(user.GatewayCustomerId);
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

            customer = await _stripeAdapter.CustomerCreateAsync(new Stripe.CustomerCreateOptions
            {
                Description = user.Name,
                Email = user.Email,
                Metadata = stripeCustomerMetadata,
                PaymentMethod = stipeCustomerPaymentMethodId,
                Source = stipeCustomerSourceToken,
                InvoiceSettings = new Stripe.CustomerInvoiceSettingsOptions
                {
                    DefaultPaymentMethod = stipeCustomerPaymentMethodId
                },
                Address = new Stripe.AddressOptions
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

        var subCreateOptions = new Stripe.SubscriptionCreateOptions
        {
            Customer = customer.Id,
            Items = new List<Stripe.SubscriptionItemOptions>(),
            Metadata = new Dictionary<string, string>
            {
                [user.GatewayIdField()] = user.Id.ToString()
            }
        };

        subCreateOptions.Items.Add(new Stripe.SubscriptionItemOptions
        {
            Plan = paymentMethodType == PaymentMethodType.AppleInApp ? PremiumPlanAppleIapId : PremiumPlanId,
            Quantity = 1,
        });

        if (!string.IsNullOrWhiteSpace(taxInfo?.BillingAddressCountry)
                && !string.IsNullOrWhiteSpace(taxInfo?.BillingAddressPostalCode))
        {
            var taxRates = await _taxRateRepository.GetByLocationAsync(
                new TaxRate()
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
            subCreateOptions.Items.Add(new Stripe.SubscriptionItemOptions
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

    private async Task<Stripe.Subscription> ChargeForNewSubscriptionAsync(ISubscriber subcriber, Stripe.Customer customer,
        bool createdStripeCustomer, bool stripePaymentMethod, PaymentMethodType paymentMethodType,
        Stripe.SubscriptionCreateOptions subCreateOptions, Braintree.Customer braintreeCustomer)
    {
        var addedCreditToStripeCustomer = false;
        Braintree.Transaction braintreeTransaction = null;
        Transaction appleTransaction = null;

        var subInvoiceMetadata = new Dictionary<string, string>();
        Stripe.Subscription subscription = null;
        try
        {
            if (!stripePaymentMethod)
            {
                var previewInvoice = await _stripeAdapter.InvoiceUpcomingAsync(new Stripe.UpcomingInvoiceOptions
                {
                    Customer = customer.Id,
                    SubscriptionItems = ToInvoiceSubscriptionItemOptions(subCreateOptions.Items),
                    SubscriptionDefaultTaxRates = subCreateOptions.DefaultTaxRates,
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

                    await _stripeAdapter.CustomerUpdateAsync(customer.Id, new Stripe.CustomerUpdateOptions
                    {
                        Balance = customer.Balance - previewInvoice.AmountDue
                    });
                    addedCreditToStripeCustomer = true;
                }
            }
            else if (paymentMethodType == PaymentMethodType.Credit)
            {
                var previewInvoice = await _stripeAdapter.InvoiceUpcomingAsync(new Stripe.UpcomingInvoiceOptions
                {
                    Customer = customer.Id,
                    SubscriptionItems = ToInvoiceSubscriptionItemOptions(subCreateOptions.Items),
                    SubscriptionDefaultTaxRates = subCreateOptions.DefaultTaxRates,
                });
                if (previewInvoice.AmountDue > 0)
                {
                    throw new GatewayException("Your account does not have enough credit available.");
                }
            }

            subCreateOptions.OffSession = true;
            subCreateOptions.AddExpand("latest_invoice.payment_intent");
            subscription = await _stripeAdapter.SubscriptionCreateAsync(subCreateOptions);
            if (subscription.Status == "incomplete" && subscription.LatestInvoice?.PaymentIntent != null)
            {
                if (subscription.LatestInvoice.PaymentIntent.Status == "requires_payment_method")
                {
                    await _stripeAdapter.SubscriptionCancelAsync(subscription.Id, new Stripe.SubscriptionCancelOptions());
                    throw new GatewayException("Payment method was declined.");
                }
            }

            if (!stripePaymentMethod && subInvoiceMetadata.Any())
            {
                var invoices = await _stripeAdapter.InvoiceListAsync(new Stripe.InvoiceListOptions
                {
                    Subscription = subscription.Id
                });

                var invoice = invoices?.FirstOrDefault();
                if (invoice == null)
                {
                    throw new GatewayException("Invoice not found.");
                }

                await _stripeAdapter.InvoiceUpdateAsync(invoice.Id, new Stripe.InvoiceUpdateOptions
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
                    await _stripeAdapter.CustomerDeleteAsync(customer.Id);
                }
                else if (addedCreditToStripeCustomer || customer.Balance < 0)
                {
                    await _stripeAdapter.CustomerUpdateAsync(customer.Id, new Stripe.CustomerUpdateOptions
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

            if (e is Stripe.StripeException strEx &&
                (strEx.StripeError?.Message?.Contains("cannot be used because it is not verified") ?? false))
            {
                throw new GatewayException("Bank account is not yet verified.");
            }

            throw;
        }
    }

    private List<Stripe.InvoiceSubscriptionItemOptions> ToInvoiceSubscriptionItemOptions(
        List<Stripe.SubscriptionItemOptions> subItemOptions)
    {
        return subItemOptions.Select(si => new Stripe.InvoiceSubscriptionItemOptions
        {
            Plan = si.Plan,
            Quantity = si.Quantity
        }).ToList();
    }

    private async Task<string> FinalizeSubscriptionChangeAsync(IStorableSubscriber storableSubscriber,
        SubscriptionUpdate subscriptionUpdate, DateTime? prorationDate)
    {
        // remember, when in doubt, throw

        var sub = await _stripeAdapter.SubscriptionGetAsync(storableSubscriber.GatewaySubscriptionId);
        if (sub == null)
        {
            throw new GatewayException("Subscription not found.");
        }

        prorationDate ??= DateTime.UtcNow;
        var collectionMethod = sub.CollectionMethod;
        var daysUntilDue = sub.DaysUntilDue;
        var chargeNow = collectionMethod == "charge_automatically";
        var updatedItemOptions = subscriptionUpdate.UpgradeItemsOptions(sub);

        var subUpdateOptions = new Stripe.SubscriptionUpdateOptions
        {
            Items = updatedItemOptions,
            ProrationBehavior = "always_invoice",
            DaysUntilDue = daysUntilDue ?? 1,
            CollectionMethod = "send_invoice",
            ProrationDate = prorationDate,
        };

        if (!subscriptionUpdate.UpdateNeeded(sub))
        {
            // No need to update subscription, quantity matches
            return null;
        }

        var customer = await _stripeAdapter.CustomerGetAsync(sub.CustomerId);

        if (!string.IsNullOrWhiteSpace(customer?.Address?.Country)
                && !string.IsNullOrWhiteSpace(customer?.Address?.PostalCode))
        {
            var taxRates = await _taxRateRepository.GetByLocationAsync(
                new TaxRate()
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

        string paymentIntentClientSecret = null;
        try
        {
            var subResponse = await _stripeAdapter.SubscriptionUpdateAsync(sub.Id, subUpdateOptions);

            var invoice = await _stripeAdapter.InvoiceGetAsync(subResponse?.LatestInvoiceId, new Stripe.InvoiceGetOptions());
            if (invoice == null)
            {
                throw new BadRequestException("Unable to locate draft invoice for subscription update.");
            }

            if (invoice.AmountDue > 0 && updatedItemOptions.Any(i => i.Quantity > 0))
            {
                try
                {
                    if (chargeNow)
                    {
                        paymentIntentClientSecret = await PayInvoiceAfterSubscriptionChangeAsync(
                            storableSubscriber, invoice);
                    }
                    else
                    {
                        invoice = await _stripeAdapter.InvoiceFinalizeInvoiceAsync(subResponse.LatestInvoiceId, new Stripe.InvoiceFinalizeOptions
                        {
                            AutoAdvance = false,
                        });
                        await _stripeAdapter.InvoiceSendInvoiceAsync(invoice.Id, new Stripe.InvoiceSendOptions());
                        paymentIntentClientSecret = null;
                    }
                }
                catch
                {
                    // Need to revert the subscription
                    await _stripeAdapter.SubscriptionUpdateAsync(sub.Id, new Stripe.SubscriptionUpdateOptions
                    {
                        Items = subscriptionUpdate.RevertItemsOptions(sub),
                        // This proration behavior prevents a false "credit" from
                        //  being applied forward to the next month's invoice
                        ProrationBehavior = "none",
                        CollectionMethod = collectionMethod,
                        DaysUntilDue = daysUntilDue,
                    });
                    throw;
                }
            }
            else if (!invoice.Paid)
            {
                // Pay invoice with no charge to customer this completes the invoice immediately without waiting the scheduled 1h
                invoice = await _stripeAdapter.InvoicePayAsync(subResponse.LatestInvoiceId);
                paymentIntentClientSecret = null;
            }

        }
        finally
        {
            // Change back the subscription collection method and/or days until due
            if (collectionMethod != "send_invoice" || daysUntilDue == null)
            {
                await _stripeAdapter.SubscriptionUpdateAsync(sub.Id, new Stripe.SubscriptionUpdateOptions
                {
                    CollectionMethod = collectionMethod,
                    DaysUntilDue = daysUntilDue,
                });
            }
        }

        return paymentIntentClientSecret;
    }

    public Task<string> AdjustSeatsAsync(Organization organization, StaticStore.Plan plan, int additionalSeats, DateTime? prorationDate = null)
    {
        return FinalizeSubscriptionChangeAsync(organization, new SeatSubscriptionUpdate(organization, plan, additionalSeats), prorationDate);
    }

    public Task<string> AdjustStorageAsync(IStorableSubscriber storableSubscriber, int additionalStorage,
        string storagePlanId, DateTime? prorationDate = null)
    {
        return FinalizeSubscriptionChangeAsync(storableSubscriber, new StorageSubscriptionUpdate(storagePlanId, additionalStorage), prorationDate);
    }

    public async Task CancelAndRecoverChargesAsync(ISubscriber subscriber)
    {
        if (!string.IsNullOrWhiteSpace(subscriber.GatewaySubscriptionId))
        {
            await _stripeAdapter.SubscriptionCancelAsync(subscriber.GatewaySubscriptionId,
                new Stripe.SubscriptionCancelOptions());
        }

        if (string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
        {
            return;
        }

        var customer = await _stripeAdapter.CustomerGetAsync(subscriber.GatewayCustomerId);
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
            var charges = await _stripeAdapter.ChargeListAsync(new Stripe.ChargeListOptions
            {
                Customer = subscriber.GatewayCustomerId
            });

            if (charges?.Data != null)
            {
                foreach (var charge in charges.Data.Where(c => c.Captured && !c.Refunded))
                {
                    await _stripeAdapter.RefundCreateAsync(new Stripe.RefundCreateOptions { Charge = charge.Id });
                }
            }
        }

        await _stripeAdapter.CustomerDeleteAsync(subscriber.GatewayCustomerId);
    }

    public async Task<string> PayInvoiceAfterSubscriptionChangeAsync(ISubscriber subscriber, Stripe.Invoice invoice)
    {
        var customerOptions = new Stripe.CustomerGetOptions();
        customerOptions.AddExpand("default_source");
        customerOptions.AddExpand("invoice_settings.default_payment_method");
        var customer = await _stripeAdapter.CustomerGetAsync(subscriber.GatewayCustomerId, customerOptions);
        var usingInAppPaymentMethod = customer.Metadata.ContainsKey("appleReceipt");
        if (usingInAppPaymentMethod)
        {
            throw new BadRequestException("Cannot perform this action with in-app purchase payment method. " +
                "Contact support.");
        }

        string paymentIntentClientSecret = null;

        // Invoice them and pay now instead of waiting until Stripe does this automatically.

        string cardPaymentMethodId = null;
        if (!customer.Metadata.ContainsKey("btCustomerId"))
        {
            var hasDefaultCardPaymentMethod = customer.InvoiceSettings?.DefaultPaymentMethod?.Type == "card";
            var hasDefaultValidSource = customer.DefaultSource != null &&
                (customer.DefaultSource is Stripe.Card || customer.DefaultSource is Stripe.BankAccount);
            if (!hasDefaultCardPaymentMethod && !hasDefaultValidSource)
            {
                cardPaymentMethodId = GetLatestCardPaymentMethod(customer.Id)?.Id;
                if (cardPaymentMethodId == null)
                {
                    // We're going to delete this draft invoice, it can't be paid
                    try
                    {
                        await _stripeAdapter.InvoiceDeleteAsync(invoice.Id);
                    }
                    catch
                    {
                        await _stripeAdapter.InvoiceFinalizeInvoiceAsync(invoice.Id, new Stripe.InvoiceFinalizeOptions
                        {
                            AutoAdvance = false
                        });
                        await _stripeAdapter.InvoiceVoidInvoiceAsync(invoice.Id);
                    }
                    throw new BadRequestException("No payment method is available.");
                }
            }
        }

        Braintree.Transaction braintreeTransaction = null;
        try
        {
            // Finalize the invoice (from Draft) w/o auto-advance so we
            //  can attempt payment manually.
            invoice = await _stripeAdapter.InvoiceFinalizeInvoiceAsync(invoice.Id, new Stripe.InvoiceFinalizeOptions
            {
                AutoAdvance = false,
            });
            var invoicePayOptions = new Stripe.InvoicePayOptions
            {
                PaymentMethod = cardPaymentMethodId,
            };
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
                invoice = await _stripeAdapter.InvoiceUpdateAsync(invoice.Id, new Stripe.InvoiceUpdateOptions
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

            try
            {
                invoice = await _stripeAdapter.InvoicePayAsync(invoice.Id, invoicePayOptions);
            }
            catch (Stripe.StripeException e)
            {
                if (e.HttpStatusCode == System.Net.HttpStatusCode.PaymentRequired &&
                    e.StripeError?.Code == "invoice_payment_intent_requires_action")
                {
                    // SCA required, get intent client secret
                    var invoiceGetOptions = new Stripe.InvoiceGetOptions();
                    invoiceGetOptions.AddExpand("payment_intent");
                    invoice = await _stripeAdapter.InvoiceGetAsync(invoice.Id, invoiceGetOptions);
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

                invoice = await _stripeAdapter.InvoiceVoidInvoiceAsync(invoice.Id, new Stripe.InvoiceVoidOptions());

                // HACK: Workaround for customer balance credit
                if (invoice.StartingBalance < 0)
                {
                    // Customer had a balance applied to this invoice. Since we can't fully trust Stripe to
                    //  credit it back to the customer (even though their docs claim they will), we need to
                    //  check that balance against the current customer balance and determine if it needs to be re-applied
                    customer = await _stripeAdapter.CustomerGetAsync(subscriber.GatewayCustomerId, customerOptions);

                    // Assumption: Customer balance should now be $0, otherwise payment would not have failed.
                    if (customer.Balance == 0)
                    {
                        await _stripeAdapter.CustomerUpdateAsync(customer.Id, new Stripe.CustomerUpdateOptions
                        {
                            Balance = invoice.StartingBalance
                        });
                    }
                }
            }

            if (e is Stripe.StripeException strEx &&
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
            var customer = await _stripeAdapter.CustomerGetAsync(subscriber.GatewayCustomerId);
            if (customer.Metadata.ContainsKey("appleReceipt"))
            {
                throw new BadRequestException("You are required to manage your subscription from the app store.");
            }
        }

        var sub = await _stripeAdapter.SubscriptionGetAsync(subscriber.GatewaySubscriptionId);
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
                await _stripeAdapter.SubscriptionUpdateAsync(sub.Id,
                    new Stripe.SubscriptionUpdateOptions { CancelAtPeriodEnd = true }) :
                await _stripeAdapter.SubscriptionCancelAsync(sub.Id, new Stripe.SubscriptionCancelOptions());
            if (!canceledSub.CanceledAt.HasValue)
            {
                throw new GatewayException("Unable to cancel subscription.");
            }
        }
        catch (Stripe.StripeException e)
        {
            if (e.Message != $"No such subscription: {subscriber.GatewaySubscriptionId}")
            {
                throw;
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

        var sub = await _stripeAdapter.SubscriptionGetAsync(subscriber.GatewaySubscriptionId);
        if (sub == null)
        {
            throw new GatewayException("Subscription was not found.");
        }

        if ((sub.Status != "active" && sub.Status != "trialing" && !sub.Status.StartsWith("incomplete")) ||
            !sub.CanceledAt.HasValue)
        {
            throw new GatewayException("Subscription is not marked for cancellation.");
        }

        var updatedSub = await _stripeAdapter.SubscriptionUpdateAsync(sub.Id,
            new Stripe.SubscriptionUpdateOptions { CancelAtPeriodEnd = false });
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

        Stripe.Customer customer = null;

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
            var options = new Stripe.CustomerGetOptions();
            options.AddExpand("sources");
            customer = await _stripeAdapter.CustomerGetAsync(subscriber.GatewayCustomerId, options);
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
                customer = await _stripeAdapter.CustomerCreateAsync(new Stripe.CustomerCreateOptions
                {
                    Description = subscriber.BillingName(),
                    Email = subscriber.BillingEmailAddress(),
                    Metadata = stripeCustomerMetadata,
                    Source = stipeCustomerSourceToken,
                    PaymentMethod = stipeCustomerPaymentMethodId,
                    InvoiceSettings = new Stripe.CustomerInvoiceSettingsOptions
                    {
                        DefaultPaymentMethod = stipeCustomerPaymentMethodId
                    },
                    Address = taxInfo == null ? null : new Stripe.AddressOptions
                    {
                        Country = taxInfo.BillingAddressCountry,
                        PostalCode = taxInfo.BillingAddressPostalCode,
                        Line1 = taxInfo.BillingAddressLine1 ?? string.Empty,
                        Line2 = taxInfo.BillingAddressLine2,
                        City = taxInfo.BillingAddressCity,
                        State = taxInfo.BillingAddressState,
                    },
                    Expand = new List<string> { "sources" },
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
                        var bankAccount = await _stripeAdapter.BankAccountCreateAsync(customer.Id, new Stripe.BankAccountCreateOptions
                        {
                            Source = paymentToken
                        });
                        defaultSourceId = bankAccount.Id;
                    }
                    else if (!string.IsNullOrWhiteSpace(stipeCustomerPaymentMethodId))
                    {
                        await _stripeAdapter.PaymentMethodAttachAsync(stipeCustomerPaymentMethodId,
                            new Stripe.PaymentMethodAttachOptions { Customer = customer.Id });
                        defaultPaymentMethodId = stipeCustomerPaymentMethodId;
                    }
                }

                if (customer.Sources != null)
                {
                    foreach (var source in customer.Sources.Where(s => s.Id != defaultSourceId))
                    {
                        if (source is Stripe.BankAccount)
                        {
                            await _stripeAdapter.BankAccountDeleteAsync(customer.Id, source.Id);
                        }
                        else if (source is Stripe.Card)
                        {
                            await _stripeAdapter.CardDeleteAsync(customer.Id, source.Id);
                        }
                    }
                }

                var cardPaymentMethods = _stripeAdapter.PaymentMethodListAutoPaging(new Stripe.PaymentMethodListOptions
                {
                    Customer = customer.Id,
                    Type = "card"
                });
                foreach (var cardMethod in cardPaymentMethods.Where(m => m.Id != defaultPaymentMethodId))
                {
                    await _stripeAdapter.PaymentMethodDetachAsync(cardMethod.Id, new Stripe.PaymentMethodDetachOptions());
                }

                customer = await _stripeAdapter.CustomerUpdateAsync(customer.Id, new Stripe.CustomerUpdateOptions
                {
                    Metadata = stripeCustomerMetadata,
                    DefaultSource = defaultSourceId,
                    InvoiceSettings = new Stripe.CustomerInvoiceSettingsOptions
                    {
                        DefaultPaymentMethod = defaultPaymentMethodId
                    },
                    Address = taxInfo == null ? null : new Stripe.AddressOptions
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
        catch
        {
            if (braintreeCustomer != null && !hadBtCustomer)
            {
                await _btGateway.Customer.DeleteAsync(braintreeCustomer.Id);
            }
            throw;
        }

        return createdCustomer;
    }

    public async Task<bool> CreditAccountAsync(ISubscriber subscriber, decimal creditAmount)
    {
        Stripe.Customer customer = null;
        var customerExists = subscriber.Gateway == GatewayType.Stripe &&
            !string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId);
        if (customerExists)
        {
            customer = await _stripeAdapter.CustomerGetAsync(subscriber.GatewayCustomerId);
        }
        else
        {
            customer = await _stripeAdapter.CustomerCreateAsync(new Stripe.CustomerCreateOptions
            {
                Email = subscriber.BillingEmailAddress(),
                Description = subscriber.BillingName(),
            });
            subscriber.Gateway = GatewayType.Stripe;
            subscriber.GatewayCustomerId = customer.Id;
        }
        await _stripeAdapter.CustomerUpdateAsync(customer.Id, new Stripe.CustomerUpdateOptions
        {
            Balance = customer.Balance - (long)(creditAmount * 100)
        });
        return !customerExists;
    }

    public async Task<BillingInfo> GetBillingAsync(ISubscriber subscriber)
    {
        var customer = await GetCustomerAsync(subscriber.GatewayCustomerId, GetCustomerPaymentOptions());
        var billingInfo = new BillingInfo
        {
            Balance = GetBillingBalance(customer),
            PaymentSource = await GetBillingPaymentSourceAsync(customer),
            Invoices = await GetBillingInvoicesAsync(customer),
            Transactions = await GetBillingTransactionsAsync(subscriber)
        };

        return billingInfo;
    }

    public async Task<BillingInfo> GetBillingBalanceAndSourceAsync(ISubscriber subscriber)
    {
        var customer = await GetCustomerAsync(subscriber.GatewayCustomerId, GetCustomerPaymentOptions());
        var billingInfo = new BillingInfo
        {
            Balance = GetBillingBalance(customer),
            PaymentSource = await GetBillingPaymentSourceAsync(customer)
        };

        return billingInfo;
    }

    public async Task<BillingInfo> GetBillingHistoryAsync(ISubscriber subscriber)
    {
        var customer = await GetCustomerAsync(subscriber.GatewayCustomerId);
        var billingInfo = new BillingInfo
        {
            Transactions = await GetBillingTransactionsAsync(subscriber),
            Invoices = await GetBillingInvoicesAsync(customer)
        };

        return billingInfo;
    }

    public async Task<SubscriptionInfo> GetSubscriptionAsync(ISubscriber subscriber)
    {
        var subscriptionInfo = new SubscriptionInfo();

        if (subscriber.IsUser() && !string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
        {
            var customer = await _stripeAdapter.CustomerGetAsync(subscriber.GatewayCustomerId);
            subscriptionInfo.UsingInAppPurchase = customer.Metadata.ContainsKey("appleReceipt");
        }

        if (!string.IsNullOrWhiteSpace(subscriber.GatewaySubscriptionId))
        {
            var sub = await _stripeAdapter.SubscriptionGetAsync(subscriber.GatewaySubscriptionId);
            if (sub != null)
            {
                subscriptionInfo.Subscription = new SubscriptionInfo.BillingSubscription(sub);
            }

            if (!sub.CanceledAt.HasValue && !string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
            {
                try
                {
                    var upcomingInvoice = await _stripeAdapter.InvoiceUpcomingAsync(
                        new Stripe.UpcomingInvoiceOptions { Customer = subscriber.GatewayCustomerId });
                    if (upcomingInvoice != null)
                    {
                        subscriptionInfo.UpcomingInvoice =
                            new SubscriptionInfo.BillingUpcomingInvoice(upcomingInvoice);
                    }
                }
                catch (Stripe.StripeException) { }
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

        var customer = await _stripeAdapter.CustomerGetAsync(subscriber.GatewayCustomerId,
            new Stripe.CustomerGetOptions { Expand = new List<string> { "tax_ids" } });

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
            var customer = await _stripeAdapter.CustomerUpdateAsync(subscriber.GatewayCustomerId, new Stripe.CustomerUpdateOptions
            {
                Address = new Stripe.AddressOptions
                {
                    Line1 = taxInfo.BillingAddressLine1 ?? string.Empty,
                    Line2 = taxInfo.BillingAddressLine2,
                    City = taxInfo.BillingAddressCity,
                    State = taxInfo.BillingAddressState,
                    PostalCode = taxInfo.BillingAddressPostalCode,
                    Country = taxInfo.BillingAddressCountry,
                },
                Expand = new List<string> { "tax_ids" }
            });

            if (!subscriber.IsUser() && customer != null)
            {
                var taxId = customer.TaxIds?.FirstOrDefault();

                if (taxId != null)
                {
                    await _stripeAdapter.TaxIdDeleteAsync(customer.Id, taxId.Id);
                }
                if (!string.IsNullOrWhiteSpace(taxInfo.TaxIdNumber) &&
                    !string.IsNullOrWhiteSpace(taxInfo.TaxIdType))
                {
                    await _stripeAdapter.TaxIdCreateAsync(customer.Id, new Stripe.TaxIdCreateOptions
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
        var stripeTaxRateOptions = new Stripe.TaxRateCreateOptions()
        {
            DisplayName = $"{taxRate.Country} - {taxRate.PostalCode}",
            Inclusive = false,
            Percentage = taxRate.Rate,
            Active = true
        };
        var stripeTaxRate = await _stripeAdapter.TaxRateCreateAsync(stripeTaxRateOptions);
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

        var updatedStripeTaxRate = await _stripeAdapter.TaxRateUpdateAsync(
                taxRate.Id,
                new Stripe.TaxRateUpdateOptions() { Active = false }
        );
        if (!updatedStripeTaxRate.Active)
        {
            taxRate.Active = false;
            await _taxRateRepository.ArchiveAsync(taxRate);
        }
    }

    private Stripe.PaymentMethod GetLatestCardPaymentMethod(string customerId)
    {
        var cardPaymentMethods = _stripeAdapter.PaymentMethodListAutoPaging(
            new Stripe.PaymentMethodListOptions { Customer = customerId, Type = "card" });
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

    private decimal GetBillingBalance(Stripe.Customer customer)
    {
        return customer != null ? customer.Balance / 100M : default;
    }

    private async Task<BillingInfo.BillingSource> GetBillingPaymentSourceAsync(Stripe.Customer customer)
    {
        if (customer == null)
        {
            return null;
        }

        if (customer.Metadata?.ContainsKey("appleReceipt") ?? false)
        {
            return new BillingInfo.BillingSource
            {
                Type = PaymentMethodType.AppleInApp
            };
        }

        if (customer.Metadata?.ContainsKey("btCustomerId") ?? false)
        {
            try
            {
                var braintreeCustomer = await _btGateway.Customer.FindAsync(
                    customer.Metadata["btCustomerId"]);
                if (braintreeCustomer?.DefaultPaymentMethod != null)
                {
                    return new BillingInfo.BillingSource(
                        braintreeCustomer.DefaultPaymentMethod);
                }
            }
            catch (Braintree.Exceptions.NotFoundException) { }
        }

        if (customer.InvoiceSettings?.DefaultPaymentMethod?.Type == "card")
        {
            return new BillingInfo.BillingSource(
                customer.InvoiceSettings.DefaultPaymentMethod);
        }

        if (customer.DefaultSource != null &&
            (customer.DefaultSource is Stripe.Card || customer.DefaultSource is Stripe.BankAccount))
        {
            return new BillingInfo.BillingSource(customer.DefaultSource);
        }

        var paymentMethod = GetLatestCardPaymentMethod(customer.Id);
        return paymentMethod != null ? new BillingInfo.BillingSource(paymentMethod) : null;
    }

    private Stripe.CustomerGetOptions GetCustomerPaymentOptions()
    {
        var customerOptions = new Stripe.CustomerGetOptions();
        customerOptions.AddExpand("default_source");
        customerOptions.AddExpand("invoice_settings.default_payment_method");
        return customerOptions;
    }

    private async Task<Stripe.Customer> GetCustomerAsync(string gatewayCustomerId, Stripe.CustomerGetOptions options = null)
    {
        if (string.IsNullOrWhiteSpace(gatewayCustomerId))
        {
            return null;
        }

        Stripe.Customer customer = null;
        try
        {
            customer = await _stripeAdapter.CustomerGetAsync(gatewayCustomerId, options);
        }
        catch (Stripe.StripeException) { }

        return customer;
    }

    private async Task<IEnumerable<BillingInfo.BillingTransaction>> GetBillingTransactionsAsync(ISubscriber subscriber)
    {
        ICollection<Transaction> transactions = null;
        if (subscriber is User)
        {
            transactions = await _transactionRepository.GetManyByUserIdAsync(subscriber.Id);
        }
        else if (subscriber is Organization)
        {
            transactions = await _transactionRepository.GetManyByOrganizationIdAsync(subscriber.Id);
        }

        return transactions?.OrderByDescending(i => i.CreationDate)
            .Select(t => new BillingInfo.BillingTransaction(t));

    }

    private async Task<IEnumerable<BillingInfo.BillingInvoice>> GetBillingInvoicesAsync(Stripe.Customer customer)
    {
        if (customer == null)
        {
            return null;
        }

        var invoices = await _stripeAdapter.InvoiceListAsync(new Stripe.InvoiceListOptions
        {
            Customer = customer.Id,
            Limit = 50
        });

        return invoices.Data.Where(i => i.Status != "void" && i.Status != "draft")
            .OrderByDescending(i => i.Created).Select(i => new BillingInfo.BillingInvoice(i));

    }
}
