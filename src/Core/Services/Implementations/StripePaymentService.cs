using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.BitStripe;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;
using Stripe;
using PaymentMethod = Stripe.PaymentMethod;
using StaticStore = Bit.Core.Models.StaticStore;
using TaxRate = Bit.Core.Entities.TaxRate;

namespace Bit.Core.Services;

public class StripePaymentService : IPaymentService
{
    private const string PremiumPlanId = "premium-annually";
    private const string StoragePlanId = "storage-gb-annually";
    private const string ProviderDiscountId = "msp-discount-35";
    private const string SecretsManagerStandaloneDiscountId = "sm-standalone";

    private readonly ITransactionRepository _transactionRepository;
    private readonly ILogger<StripePaymentService> _logger;
    private readonly Braintree.IBraintreeGateway _btGateway;
    private readonly ITaxRateRepository _taxRateRepository;
    private readonly IStripeAdapter _stripeAdapter;
    private readonly IGlobalSettings _globalSettings;
    private readonly IFeatureService _featureService;

    public StripePaymentService(
        ITransactionRepository transactionRepository,
        ILogger<StripePaymentService> logger,
        ITaxRateRepository taxRateRepository,
        IStripeAdapter stripeAdapter,
        Braintree.IBraintreeGateway braintreeGateway,
        IGlobalSettings globalSettings,
        IFeatureService featureService)
    {
        _transactionRepository = transactionRepository;
        _logger = logger;
        _taxRateRepository = taxRateRepository;
        _stripeAdapter = stripeAdapter;
        _btGateway = braintreeGateway;
        _globalSettings = globalSettings;
        _featureService = featureService;
    }

    public async Task<string> PurchaseOrganizationAsync(Organization org, PaymentMethodType paymentMethodType,
        string paymentToken, StaticStore.Plan plan, short additionalStorageGb,
        int additionalSeats, bool premiumAccessAddon, TaxInfo taxInfo, bool provider = false,
        int additionalSmSeats = 0, int additionalServiceAccount = 0, bool signupIsFromSecretsManagerTrial = false)
    {
        Braintree.Customer braintreeCustomer = null;
        string stipeCustomerSourceToken = null;
        string stipeCustomerPaymentMethodId = null;
        var stripeCustomerMetadata = new Dictionary<string, string>
        {
            { "region", _globalSettings.BaseServiceUri.CloudRegion }
        };
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
                    [org.BraintreeIdField()] = org.Id.ToString(),
                    [org.BraintreeCloudRegionField()] = _globalSettings.BaseServiceUri.CloudRegion
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

        var subCreateOptions = new OrganizationPurchaseSubscriptionOptions(org, plan, taxInfo, additionalSeats, additionalStorageGb, premiumAccessAddon
        , additionalSmSeats, additionalServiceAccount);

        Customer customer = null;
        Subscription subscription;
        try
        {
            var customerCreateOptions = new CustomerCreateOptions
            {
                Description = org.DisplayBusinessName(),
                Email = org.BillingEmail,
                Source = stipeCustomerSourceToken,
                PaymentMethod = stipeCustomerPaymentMethodId,
                Metadata = stripeCustomerMetadata,
                InvoiceSettings = new CustomerInvoiceSettingsOptions
                {
                    DefaultPaymentMethod = stipeCustomerPaymentMethodId,
                    CustomFields =
                    [
                        new CustomerInvoiceSettingsCustomFieldOptions
                        {
                            Name = org.SubscriberType(),
                            Value = GetFirstThirtyCharacters(org.SubscriberName()),
                        }
                    ],
                },
                Coupon = signupIsFromSecretsManagerTrial
                    ? SecretsManagerStandaloneDiscountId
                    : provider
                        ? ProviderDiscountId
                        : null,
                Address = new AddressOptions
                {
                    Country = taxInfo?.BillingAddressCountry,
                    PostalCode = taxInfo?.BillingAddressPostalCode,
                    // Line1 is required in Stripe's API, suggestion in Docs is to use Business Name instead.
                    Line1 = taxInfo?.BillingAddressLine1 ?? string.Empty,
                    Line2 = taxInfo?.BillingAddressLine2,
                    City = taxInfo?.BillingAddressCity,
                    State = taxInfo?.BillingAddressState,
                },
                TaxIdData = taxInfo?.HasTaxId != true
                    ? null
                    :
                    [
                        new CustomerTaxIdDataOptions { Type = taxInfo.TaxIdType, Value = taxInfo.TaxIdNumber, }
                    ],
            };

            customerCreateOptions.AddExpand("tax");

            customer = await _stripeAdapter.CustomerCreateAsync(customerCreateOptions);
            subCreateOptions.AddExpand("latest_invoice.payment_intent");
            subCreateOptions.Customer = customer.Id;

            if (CustomerHasTaxLocationVerified(customer))
            {
                subCreateOptions.AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true };
            }

            subscription = await _stripeAdapter.SubscriptionCreateAsync(subCreateOptions);
            if (subscription.Status == "incomplete" && subscription.LatestInvoice?.PaymentIntent != null)
            {
                if (subscription.LatestInvoice.PaymentIntent.Status == "requires_payment_method")
                {
                    await _stripeAdapter.SubscriptionCancelAsync(subscription.Id, new SubscriptionCancelOptions());
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

    public async Task<string> PurchaseOrganizationNoPaymentMethod(Organization org, StaticStore.Plan plan, int additionalSeats, bool premiumAccessAddon,
        int additionalSmSeats = 0, int additionalServiceAccount = 0, bool signupIsFromSecretsManagerTrial = false)
    {

        var stripeCustomerMetadata = new Dictionary<string, string>
        {
            { "region", _globalSettings.BaseServiceUri.CloudRegion }
        };
        var subCreateOptions = new OrganizationPurchaseSubscriptionOptions(org, plan, new TaxInfo(), additionalSeats, 0, premiumAccessAddon
        , additionalSmSeats, additionalServiceAccount);

        Customer customer = null;
        Subscription subscription;
        try
        {
            var customerCreateOptions = new CustomerCreateOptions
            {
                Description = org.DisplayBusinessName(),
                Email = org.BillingEmail,
                Metadata = stripeCustomerMetadata,
                InvoiceSettings = new CustomerInvoiceSettingsOptions
                {
                    CustomFields =
                    [
                        new CustomerInvoiceSettingsCustomFieldOptions
                        {
                            Name = org.SubscriberType(),
                            Value = GetFirstThirtyCharacters(org.SubscriberName()),
                        }
                    ],
                },
                Coupon = signupIsFromSecretsManagerTrial
                    ? SecretsManagerStandaloneDiscountId
                    : null,
                TaxIdData = null,
            };

            customer = await _stripeAdapter.CustomerCreateAsync(customerCreateOptions);
            subCreateOptions.AddExpand("latest_invoice.payment_intent");
            subCreateOptions.Customer = customer.Id;

            subscription = await _stripeAdapter.SubscriptionCreateAsync(subCreateOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating customer, walking back operation.");
            if (customer != null)
            {
                await _stripeAdapter.CustomerDeleteAsync(customer.Id);
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

        org.Enabled = true;
        org.ExpirationDate = subscription.CurrentPeriodEnd;
        return null;

    }

    private async Task ChangeOrganizationSponsorship(
        Organization org,
        OrganizationSponsorship sponsorship,
        bool applySponsorship)
    {
        var existingPlan = Utilities.StaticStore.GetPlan(org.PlanType);
        var sponsoredPlan = sponsorship?.PlanSponsorshipType != null ?
            Utilities.StaticStore.GetSponsoredPlan(sponsorship.PlanSponsorshipType.Value) :
            null;
        var subscriptionUpdate = new SponsorOrganizationSubscriptionUpdate(existingPlan, sponsoredPlan, applySponsorship);

        await FinalizeSubscriptionChangeAsync(org, subscriptionUpdate, true);

        var sub = await _stripeAdapter.SubscriptionGetAsync(org.GatewaySubscriptionId);
        org.ExpirationDate = sub.CurrentPeriodEnd;

        if (sponsorship is not null)
        {
            sponsorship.ValidUntil = sub.CurrentPeriodEnd;
        }
    }

    public Task SponsorOrganizationAsync(Organization org, OrganizationSponsorship sponsorship) =>
        ChangeOrganizationSponsorship(org, sponsorship, true);

    public Task RemoveOrganizationSponsorshipAsync(Organization org, OrganizationSponsorship sponsorship) =>
        ChangeOrganizationSponsorship(org, sponsorship, false);

    public async Task<string> UpgradeFreeOrganizationAsync(Organization org, StaticStore.Plan plan,
        OrganizationUpgrade upgrade)
    {
        if (!string.IsNullOrWhiteSpace(org.GatewaySubscriptionId))
        {
            throw new BadRequestException("Organization already has a subscription.");
        }

        var customerOptions = new CustomerGetOptions();
        customerOptions.AddExpand("default_source");
        customerOptions.AddExpand("invoice_settings.default_payment_method");
        customerOptions.AddExpand("tax");
        var customer = await _stripeAdapter.CustomerGetAsync(org.GatewayCustomerId, customerOptions);
        if (customer == null)
        {
            throw new GatewayException("Could not find customer payment profile.");
        }

        if (!string.IsNullOrEmpty(upgrade.TaxInfo?.BillingAddressCountry) &&
            !string.IsNullOrEmpty(upgrade.TaxInfo?.BillingAddressPostalCode))
        {
            var addressOptions = new AddressOptions
            {
                Country = upgrade.TaxInfo.BillingAddressCountry,
                PostalCode = upgrade.TaxInfo.BillingAddressPostalCode,
                // Line1 is required in Stripe's API, suggestion in Docs is to use Business Name instead.
                Line1 = upgrade.TaxInfo.BillingAddressLine1 ?? string.Empty,
                Line2 = upgrade.TaxInfo.BillingAddressLine2,
                City = upgrade.TaxInfo.BillingAddressCity,
                State = upgrade.TaxInfo.BillingAddressState,
            };
            var customerUpdateOptions = new CustomerUpdateOptions { Address = addressOptions };
            customerUpdateOptions.AddExpand("default_source");
            customerUpdateOptions.AddExpand("invoice_settings.default_payment_method");
            customerUpdateOptions.AddExpand("tax");
            customer = await _stripeAdapter.CustomerUpdateAsync(org.GatewayCustomerId, customerUpdateOptions);
        }

        var subCreateOptions = new OrganizationUpgradeSubscriptionOptions(customer.Id, org, plan, upgrade);

        if (CustomerHasTaxLocationVerified(customer))
        {
            subCreateOptions.DefaultTaxRates = [];
            subCreateOptions.AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true };
        }

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
            Customer customer, SubscriptionCreateOptions subCreateOptions)
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
        if (paymentMethodType is PaymentMethodType.BankAccount)
        {
            throw new GatewayException("Payment method is not supported at this time.");
        }

        var createdStripeCustomer = false;
        Customer customer = null;
        Braintree.Customer braintreeCustomer = null;
        var stripePaymentMethod = paymentMethodType is PaymentMethodType.Card or PaymentMethodType.BankAccount
            or PaymentMethodType.Credit;

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
                await UpdatePaymentMethodAsync(user, paymentMethodType, paymentToken, taxInfo);
            }

            try
            {
                var customerGetOptions = new CustomerGetOptions();
                customerGetOptions.AddExpand("tax");
                customer = await _stripeAdapter.CustomerGetAsync(user.GatewayCustomerId, customerGetOptions);
            }
            catch
            {
                _logger.LogWarning(
                    "Attempted to get existing customer from Stripe, but customer ID was not found. Attempting to recreate customer...");
            }
        }

        if (customer == null && !string.IsNullOrWhiteSpace(paymentToken))
        {
            var stripeCustomerMetadata = new Dictionary<string, string>
            {
                { "region", _globalSettings.BaseServiceUri.CloudRegion }
            };
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
                        [user.BraintreeIdField()] = user.Id.ToString(),
                        [user.BraintreeCloudRegionField()] = _globalSettings.BaseServiceUri.CloudRegion
                    }
                });

                if (!customerResult.IsSuccess() || customerResult.Target.PaymentMethods.Length == 0)
                {
                    throw new GatewayException("Failed to create PayPal customer record.");
                }

                braintreeCustomer = customerResult.Target;
                stripeCustomerMetadata.Add("btCustomerId", braintreeCustomer.Id);
            }
            else if (!stripePaymentMethod)
            {
                throw new GatewayException("Payment method is not supported at this time.");
            }

            var customerCreateOptions = new CustomerCreateOptions
            {
                Description = user.Name,
                Email = user.Email,
                Metadata = stripeCustomerMetadata,
                PaymentMethod = stipeCustomerPaymentMethodId,
                Source = stipeCustomerSourceToken,
                InvoiceSettings = new CustomerInvoiceSettingsOptions
                {
                    DefaultPaymentMethod = stipeCustomerPaymentMethodId,
                    CustomFields =
                    [
                        new CustomerInvoiceSettingsCustomFieldOptions()
                        {
                            Name = user.SubscriberType(),
                            Value = GetFirstThirtyCharacters(user.SubscriberName()),
                        }

                    ]
                },
                Address = new AddressOptions
                {
                    Line1 = string.Empty,
                    Country = taxInfo.BillingAddressCountry,
                    PostalCode = taxInfo.BillingAddressPostalCode,
                },
            };
            customerCreateOptions.AddExpand("tax");
            customer = await _stripeAdapter.CustomerCreateAsync(customerCreateOptions);
            createdStripeCustomer = true;
        }

        if (customer == null)
        {
            throw new GatewayException("Could not set up customer payment profile.");
        }

        var subCreateOptions = new SubscriptionCreateOptions
        {
            Customer = customer.Id,
            Items = [],
            Metadata = new Dictionary<string, string>
            {
                [user.GatewayIdField()] = user.Id.ToString()
            }
        };

        subCreateOptions.Items.Add(new SubscriptionItemOptions
        {
            Plan = PremiumPlanId,
            Quantity = 1
        });

        if (additionalStorageGb > 0)
        {
            subCreateOptions.Items.Add(new SubscriptionItemOptions
            {
                Plan = StoragePlanId,
                Quantity = additionalStorageGb
            });
        }

        if (CustomerHasTaxLocationVerified(customer))
        {
            subCreateOptions.DefaultTaxRates = [];
            subCreateOptions.AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true };
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

        user.Premium = true;
        user.PremiumExpirationDate = subscription.CurrentPeriodEnd;
        return null;
    }

    private async Task<Subscription> ChargeForNewSubscriptionAsync(ISubscriber subscriber, Customer customer,
        bool createdStripeCustomer, bool stripePaymentMethod, PaymentMethodType paymentMethodType,
        SubscriptionCreateOptions subCreateOptions, Braintree.Customer braintreeCustomer)
    {
        var addedCreditToStripeCustomer = false;
        Braintree.Transaction braintreeTransaction = null;

        var subInvoiceMetadata = new Dictionary<string, string>();
        Subscription subscription = null;
        try
        {
            if (!stripePaymentMethod)
            {
                var previewInvoice = await _stripeAdapter.InvoiceUpcomingAsync(new UpcomingInvoiceOptions
                {
                    Customer = customer.Id,
                    SubscriptionItems = ToInvoiceSubscriptionItemOptions(subCreateOptions.Items)
                });

                if (CustomerHasTaxLocationVerified(customer))
                {
                    previewInvoice.AutomaticTax = new InvoiceAutomaticTax { Enabled = true };
                }

                if (previewInvoice.AmountDue > 0)
                {
                    var braintreeCustomerId = customer.Metadata != null &&
                        customer.Metadata.ContainsKey("btCustomerId") ? customer.Metadata["btCustomerId"] : null;
                    if (!string.IsNullOrWhiteSpace(braintreeCustomerId))
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
                                        CustomField = $"{subscriber.BraintreeIdField()}:{subscriber.Id},{subscriber.BraintreeCloudRegionField()}:{_globalSettings.BaseServiceUri.CloudRegion}"
                                    }
                                },
                                CustomFields = new Dictionary<string, string>
                                {
                                    [subscriber.BraintreeIdField()] = subscriber.Id.ToString(),
                                    [subscriber.BraintreeCloudRegionField()] = _globalSettings.BaseServiceUri.CloudRegion
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

                    await _stripeAdapter.CustomerUpdateAsync(customer.Id, new CustomerUpdateOptions
                    {
                        Balance = customer.Balance - previewInvoice.AmountDue
                    });
                    addedCreditToStripeCustomer = true;
                }
            }
            else if (paymentMethodType == PaymentMethodType.Credit)
            {
                var upcomingInvoiceOptions = new UpcomingInvoiceOptions
                {
                    Customer = customer.Id,
                    SubscriptionItems = ToInvoiceSubscriptionItemOptions(subCreateOptions.Items),
                    SubscriptionDefaultTaxRates = subCreateOptions.DefaultTaxRates,
                };

                if (CustomerHasTaxLocationVerified(customer))
                {
                    upcomingInvoiceOptions.AutomaticTax = new InvoiceAutomaticTaxOptions { Enabled = true };
                    upcomingInvoiceOptions.SubscriptionDefaultTaxRates = [];
                }

                var previewInvoice = await _stripeAdapter.InvoiceUpcomingAsync(upcomingInvoiceOptions);

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
                    await _stripeAdapter.SubscriptionCancelAsync(subscription.Id, new SubscriptionCancelOptions());
                    throw new GatewayException("Payment method was declined.");
                }
            }

            if (!stripePaymentMethod && subInvoiceMetadata.Any())
            {
                var invoices = await _stripeAdapter.InvoiceListAsync(new StripeInvoiceListOptions
                {
                    Subscription = subscription.Id
                });

                var invoice = invoices?.FirstOrDefault();
                if (invoice == null)
                {
                    throw new GatewayException("Invoice not found.");
                }

                await _stripeAdapter.InvoiceUpdateAsync(invoice.Id, new InvoiceUpdateOptions
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
                    await _stripeAdapter.CustomerUpdateAsync(customer.Id, new CustomerUpdateOptions
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

            if (e is StripeException strEx &&
                (strEx.StripeError?.Message?.Contains("cannot be used because it is not verified") ?? false))
            {
                throw new GatewayException("Bank account is not yet verified.");
            }

            throw;
        }
    }

    private List<InvoiceSubscriptionItemOptions> ToInvoiceSubscriptionItemOptions(
        List<SubscriptionItemOptions> subItemOptions)
    {
        return subItemOptions.Select(si => new InvoiceSubscriptionItemOptions
        {
            Plan = si.Plan,
            Price = si.Price,
            Quantity = si.Quantity,
            Id = si.Id
        }).ToList();
    }

    private async Task<string> FinalizeSubscriptionChangeAsync(ISubscriber subscriber,
        SubscriptionUpdate subscriptionUpdate, bool invoiceNow = false)
    {
        // remember, when in doubt, throw
        var subGetOptions = new SubscriptionGetOptions();
        // subGetOptions.AddExpand("customer");
        subGetOptions.AddExpand("customer.tax");
        var sub = await _stripeAdapter.SubscriptionGetAsync(subscriber.GatewaySubscriptionId, subGetOptions);
        if (sub == null)
        {
            throw new GatewayException("Subscription not found.");
        }

        if (sub.Status == SubscriptionStatuses.Canceled)
        {
            throw new BadRequestException("You do not have an active subscription. Reinstate your subscription to make changes.");
        }

        var collectionMethod = sub.CollectionMethod;
        var daysUntilDue = sub.DaysUntilDue;
        var chargeNow = collectionMethod == "charge_automatically";
        var updatedItemOptions = subscriptionUpdate.UpgradeItemsOptions(sub);
        var isAnnualPlan = sub?.Items?.Data.FirstOrDefault()?.Plan?.Interval == "year";

        var subUpdateOptions = new SubscriptionUpdateOptions
        {
            Items = updatedItemOptions,
            ProrationBehavior = invoiceNow ? Constants.AlwaysInvoice : Constants.CreateProrations,
            DaysUntilDue = daysUntilDue ?? 1,
            CollectionMethod = "send_invoice"
        };
        if (!invoiceNow && isAnnualPlan && sub.Status.Trim() != "trialing")
        {
            subUpdateOptions.PendingInvoiceItemInterval =
                new SubscriptionPendingInvoiceItemIntervalOptions { Interval = "month" };
        }

        if (sub.AutomaticTax.Enabled != true &&
            CustomerHasTaxLocationVerified(sub.Customer))
        {
            subUpdateOptions.DefaultTaxRates = [];
            subUpdateOptions.AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true };
        }

        if (!subscriptionUpdate.UpdateNeeded(sub))
        {
            // No need to update subscription, quantity matches
            return null;
        }

        string paymentIntentClientSecret = null;
        try
        {
            var subResponse = await _stripeAdapter.SubscriptionUpdateAsync(sub.Id, subUpdateOptions);

            var invoice = await _stripeAdapter.InvoiceGetAsync(subResponse?.LatestInvoiceId, new InvoiceGetOptions());
            if (invoice == null)
            {
                throw new BadRequestException("Unable to locate draft invoice for subscription update.");
            }

            if (invoice.AmountDue > 0 && updatedItemOptions.Any(i => i.Quantity > 0))
            {
                try
                {
                    if (invoiceNow)
                    {
                        if (chargeNow)
                        {
                            paymentIntentClientSecret =
                                await PayInvoiceAfterSubscriptionChangeAsync(subscriber, invoice);
                        }
                        else
                        {
                            invoice = await _stripeAdapter.InvoiceFinalizeInvoiceAsync(subResponse.LatestInvoiceId,
                                new InvoiceFinalizeOptions { AutoAdvance = false, });
                            await _stripeAdapter.InvoiceSendInvoiceAsync(invoice.Id, new InvoiceSendOptions());
                            paymentIntentClientSecret = null;
                        }
                    }
                }
                catch
                {
                    // Need to revert the subscription
                    await _stripeAdapter.SubscriptionUpdateAsync(sub.Id, new SubscriptionUpdateOptions
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
                await _stripeAdapter.SubscriptionUpdateAsync(sub.Id, new SubscriptionUpdateOptions
                {
                    CollectionMethod = collectionMethod,
                    DaysUntilDue = daysUntilDue,
                });
            }
        }

        return paymentIntentClientSecret;
    }

    public Task<string> AdjustSubscription(
        Organization organization,
        StaticStore.Plan updatedPlan,
        int newlyPurchasedPasswordManagerSeats,
        bool subscribedToSecretsManager,
        int? newlyPurchasedSecretsManagerSeats,
        int? newlyPurchasedAdditionalSecretsManagerServiceAccounts,
        int newlyPurchasedAdditionalStorage) =>
        FinalizeSubscriptionChangeAsync(
            organization,
            new CompleteSubscriptionUpdate(
                organization,
                new SubscriptionData
                {
                    Plan = updatedPlan,
                    PurchasedPasswordManagerSeats = newlyPurchasedPasswordManagerSeats,
                    SubscribedToSecretsManager = subscribedToSecretsManager,
                    PurchasedSecretsManagerSeats = newlyPurchasedSecretsManagerSeats,
                    PurchasedAdditionalSecretsManagerServiceAccounts =
                        newlyPurchasedAdditionalSecretsManagerServiceAccounts,
                    PurchasedAdditionalStorage = newlyPurchasedAdditionalStorage
                }), true);

    public Task<string> AdjustSeatsAsync(Organization organization, StaticStore.Plan plan, int additionalSeats) =>
        FinalizeSubscriptionChangeAsync(organization, new SeatSubscriptionUpdate(organization, plan, additionalSeats));

    public Task<string> AdjustSeats(
        Provider provider,
        StaticStore.Plan plan,
        int currentlySubscribedSeats,
        int newlySubscribedSeats)
        => FinalizeSubscriptionChangeAsync(
            provider,
            new ProviderSubscriptionUpdate(
                plan.Type,
                currentlySubscribedSeats,
                newlySubscribedSeats));

    public Task<string> AdjustSmSeatsAsync(Organization organization, StaticStore.Plan plan, int additionalSeats) =>
        FinalizeSubscriptionChangeAsync(
            organization,
            new SmSeatSubscriptionUpdate(organization, plan, additionalSeats));

    public Task<string> AdjustServiceAccountsAsync(
        Organization organization,
        StaticStore.Plan plan,
        int additionalServiceAccounts) =>
        FinalizeSubscriptionChangeAsync(
            organization,
            new ServiceAccountSubscriptionUpdate(organization, plan, additionalServiceAccounts));

    public Task<string> AdjustStorageAsync(
        IStorableSubscriber storableSubscriber,
        int additionalStorage,
        string storagePlanId)
    {
        return FinalizeSubscriptionChangeAsync(
            storableSubscriber,
            new StorageSubscriptionUpdate(storagePlanId, additionalStorage));
    }

    public async Task CancelAndRecoverChargesAsync(ISubscriber subscriber)
    {
        if (!string.IsNullOrWhiteSpace(subscriber.GatewaySubscriptionId))
        {
            await _stripeAdapter.SubscriptionCancelAsync(subscriber.GatewaySubscriptionId,
                new SubscriptionCancelOptions());
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
            var charges = await _stripeAdapter.ChargeListAsync(new ChargeListOptions
            {
                Customer = subscriber.GatewayCustomerId
            });

            if (charges?.Data != null)
            {
                foreach (var charge in charges.Data.Where(c => c.Captured && !c.Refunded))
                {
                    await _stripeAdapter.RefundCreateAsync(new RefundCreateOptions { Charge = charge.Id });
                }
            }
        }

        await _stripeAdapter.CustomerDeleteAsync(subscriber.GatewayCustomerId);
    }

    public async Task<string> PayInvoiceAfterSubscriptionChangeAsync(ISubscriber subscriber, Invoice invoice)
    {
        var customerOptions = new CustomerGetOptions();
        customerOptions.AddExpand("default_source");
        customerOptions.AddExpand("invoice_settings.default_payment_method");
        var customer = await _stripeAdapter.CustomerGetAsync(subscriber.GatewayCustomerId, customerOptions);

        string paymentIntentClientSecret = null;

        // Invoice them and pay now instead of waiting until Stripe does this automatically.

        string cardPaymentMethodId = null;
        if (!customer.Metadata.ContainsKey("btCustomerId"))
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
                    try
                    {
                        await _stripeAdapter.InvoiceDeleteAsync(invoice.Id);
                    }
                    catch
                    {
                        await _stripeAdapter.InvoiceFinalizeInvoiceAsync(invoice.Id, new InvoiceFinalizeOptions
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
            invoice = await _stripeAdapter.InvoiceFinalizeInvoiceAsync(invoice.Id, new InvoiceFinalizeOptions
            {
                AutoAdvance = false,
            });
            var invoicePayOptions = new InvoicePayOptions
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
                                CustomField = $"{subscriber.BraintreeIdField()}:{subscriber.Id},{subscriber.BraintreeCloudRegionField()}:{_globalSettings.BaseServiceUri.CloudRegion}"
                            }
                        },
                        CustomFields = new Dictionary<string, string>
                        {
                            [subscriber.BraintreeIdField()] = subscriber.Id.ToString(),
                            [subscriber.BraintreeCloudRegionField()] = _globalSettings.BaseServiceUri.CloudRegion
                        }
                    });

                if (!transactionResult.IsSuccess())
                {
                    throw new GatewayException("Failed to charge PayPal customer.");
                }

                braintreeTransaction = transactionResult.Target;
                invoice = await _stripeAdapter.InvoiceUpdateAsync(invoice.Id, new InvoiceUpdateOptions
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
            catch (StripeException e)
            {
                if (e.HttpStatusCode == System.Net.HttpStatusCode.PaymentRequired &&
                    e.StripeError?.Code == "invoice_payment_intent_requires_action")
                {
                    // SCA required, get intent client secret
                    var invoiceGetOptions = new InvoiceGetOptions();
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

                invoice = await _stripeAdapter.InvoiceVoidInvoiceAsync(invoice.Id, new InvoiceVoidOptions());

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
                        await _stripeAdapter.CustomerUpdateAsync(customer.Id, new CustomerUpdateOptions
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

    public async Task CancelSubscriptionAsync(ISubscriber subscriber, bool endOfPeriod = false)
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
                    new SubscriptionUpdateOptions { CancelAtPeriodEnd = true }) :
                await _stripeAdapter.SubscriptionCancelAsync(sub.Id, new SubscriptionCancelOptions());
            if (!canceledSub.CanceledAt.HasValue)
            {
                throw new GatewayException("Unable to cancel subscription.");
            }
        }
        catch (StripeException e)
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
            new SubscriptionUpdateOptions { CancelAtPeriodEnd = false });
        if (updatedSub.CanceledAt.HasValue)
        {
            throw new GatewayException("Unable to reinstate subscription.");
        }
    }

    public async Task<bool> UpdatePaymentMethodAsync(ISubscriber subscriber, PaymentMethodType paymentMethodType,
        string paymentToken, TaxInfo taxInfo = null)
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
        Braintree.Customer braintreeCustomer = null;
        string stipeCustomerSourceToken = null;
        string stipeCustomerPaymentMethodId = null;
        var stripeCustomerMetadata = new Dictionary<string, string>
        {
            { "region", _globalSettings.BaseServiceUri.CloudRegion }
        };
        var stripePaymentMethod = paymentMethodType is PaymentMethodType.Card or PaymentMethodType.BankAccount;

        Customer customer = null;

        if (!string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
        {
            var options = new CustomerGetOptions { Expand = ["sources", "tax", "subscriptions"] };
            customer = await _stripeAdapter.CustomerGetAsync(subscriber.GatewayCustomerId, options);
            if (customer.Metadata?.Any() ?? false)
            {
                stripeCustomerMetadata = customer.Metadata;
            }
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
                        [subscriber.BraintreeIdField()] = subscriber.Id.ToString(),
                        [subscriber.BraintreeCloudRegionField()] = _globalSettings.BaseServiceUri.CloudRegion
                    }
                });

                if (!customerResult.IsSuccess() || customerResult.Target.PaymentMethods.Length == 0)
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

        if (stripeCustomerMetadata.ContainsKey("btCustomerId"))
        {
            if (braintreeCustomer?.Id != stripeCustomerMetadata["btCustomerId"])
            {
                stripeCustomerMetadata["btCustomerId_old"] = stripeCustomerMetadata["btCustomerId"];
            }

            stripeCustomerMetadata["btCustomerId"] = braintreeCustomer?.Id;
        }
        else if (!string.IsNullOrWhiteSpace(braintreeCustomer?.Id))
        {
            stripeCustomerMetadata.Add("btCustomerId", braintreeCustomer.Id);
        }

        try
        {
            if (customer == null)
            {
                customer = await _stripeAdapter.CustomerCreateAsync(new CustomerCreateOptions
                {
                    Description = subscriber.BillingName(),
                    Email = subscriber.BillingEmailAddress(),
                    Metadata = stripeCustomerMetadata,
                    Source = stipeCustomerSourceToken,
                    PaymentMethod = stipeCustomerPaymentMethodId,
                    InvoiceSettings = new CustomerInvoiceSettingsOptions
                    {
                        DefaultPaymentMethod = stipeCustomerPaymentMethodId,
                        CustomFields =
                        [
                            new CustomerInvoiceSettingsCustomFieldOptions()
                            {
                                Name = subscriber.SubscriberType(),
                                Value = GetFirstThirtyCharacters(subscriber.SubscriberName()),
                            }

                        ]
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
                    Expand = ["sources", "tax", "subscriptions"],
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
                        var bankAccount = await _stripeAdapter.BankAccountCreateAsync(customer.Id, new BankAccountCreateOptions
                        {
                            Source = paymentToken
                        });
                        defaultSourceId = bankAccount.Id;
                    }
                    else if (!string.IsNullOrWhiteSpace(stipeCustomerPaymentMethodId))
                    {
                        await _stripeAdapter.PaymentMethodAttachAsync(stipeCustomerPaymentMethodId,
                            new PaymentMethodAttachOptions { Customer = customer.Id });
                        defaultPaymentMethodId = stipeCustomerPaymentMethodId;
                    }
                }

                if (customer.Sources != null)
                {
                    foreach (var source in customer.Sources.Where(s => s.Id != defaultSourceId))
                    {
                        if (source is BankAccount)
                        {
                            await _stripeAdapter.BankAccountDeleteAsync(customer.Id, source.Id);
                        }
                        else if (source is Card)
                        {
                            await _stripeAdapter.CardDeleteAsync(customer.Id, source.Id);
                        }
                    }
                }

                var cardPaymentMethods = _stripeAdapter.PaymentMethodListAutoPaging(new PaymentMethodListOptions
                {
                    Customer = customer.Id,
                    Type = "card"
                });
                foreach (var cardMethod in cardPaymentMethods.Where(m => m.Id != defaultPaymentMethodId))
                {
                    await _stripeAdapter.PaymentMethodDetachAsync(cardMethod.Id, new PaymentMethodDetachOptions());
                }

                customer = await _stripeAdapter.CustomerUpdateAsync(customer.Id, new CustomerUpdateOptions
                {
                    Metadata = stripeCustomerMetadata,
                    DefaultSource = defaultSourceId,
                    InvoiceSettings = new CustomerInvoiceSettingsOptions
                    {
                        DefaultPaymentMethod = defaultPaymentMethodId,
                        CustomFields =
                        [
                            new CustomerInvoiceSettingsCustomFieldOptions()
                            {
                                Name = subscriber.SubscriberType(),
                                Value = GetFirstThirtyCharacters(subscriber.SubscriberName())
                            }
                        ]
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
                    Expand = ["tax", "subscriptions"]
                });
            }

            if (!string.IsNullOrEmpty(subscriber.GatewaySubscriptionId) &&
                customer.Subscriptions.Any(sub =>
                    sub.Id == subscriber.GatewaySubscriptionId &&
                    !sub.AutomaticTax.Enabled) &&
                CustomerHasTaxLocationVerified(customer))
            {
                var subscriptionUpdateOptions = new SubscriptionUpdateOptions
                {
                    AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true },
                    DefaultTaxRates = []
                };

                _ = await _stripeAdapter.SubscriptionUpdateAsync(
                    subscriber.GatewaySubscriptionId,
                    subscriptionUpdateOptions);
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
        Customer customer = null;
        var customerExists = subscriber.Gateway == GatewayType.Stripe &&
            !string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId);
        if (customerExists)
        {
            customer = await _stripeAdapter.CustomerGetAsync(subscriber.GatewayCustomerId);
        }
        else
        {
            customer = await _stripeAdapter.CustomerCreateAsync(new CustomerCreateOptions
            {
                Email = subscriber.BillingEmailAddress(),
                Description = subscriber.BillingName(),
            });
            subscriber.Gateway = GatewayType.Stripe;
            subscriber.GatewayCustomerId = customer.Id;
        }
        await _stripeAdapter.CustomerUpdateAsync(customer.Id, new CustomerUpdateOptions
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
            PaymentSource = await GetBillingPaymentSourceAsync(customer)
        };

        return billingInfo;
    }

    public async Task<BillingHistoryInfo> GetBillingHistoryAsync(ISubscriber subscriber)
    {
        var customer = await GetCustomerAsync(subscriber.GatewayCustomerId);
        var billingInfo = new BillingHistoryInfo
        {
            Transactions = await GetBillingTransactionsAsync(subscriber, 20),
            Invoices = await GetBillingInvoicesAsync(customer, 20)
        };

        return billingInfo;
    }

    public async Task<SubscriptionInfo> GetSubscriptionAsync(ISubscriber subscriber)
    {
        var subscriptionInfo = new SubscriptionInfo();

        if (!string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
        {
            var customerGetOptions = new CustomerGetOptions();
            customerGetOptions.AddExpand("discount.coupon.applies_to");
            var customer = await _stripeAdapter.CustomerGetAsync(subscriber.GatewayCustomerId, customerGetOptions);

            if (customer.Discount != null)
            {
                subscriptionInfo.CustomerDiscount = new SubscriptionInfo.BillingCustomerDiscount(customer.Discount);
            }
        }

        if (string.IsNullOrWhiteSpace(subscriber.GatewaySubscriptionId))
        {
            return subscriptionInfo;
        }

        var sub = await _stripeAdapter.SubscriptionGetAsync(subscriber.GatewaySubscriptionId, new SubscriptionGetOptions
        {
            Expand = ["test_clock"]
        });

        if (sub != null)
        {
            subscriptionInfo.Subscription = new SubscriptionInfo.BillingSubscription(sub);

            if (_featureService.IsEnabled(FeatureFlagKeys.AC1795_UpdatedSubscriptionStatusSection))
            {
                var (suspensionDate, unpaidPeriodEndDate) = await GetSuspensionDateAsync(sub);

                if (suspensionDate.HasValue && unpaidPeriodEndDate.HasValue)
                {
                    subscriptionInfo.Subscription.SuspensionDate = suspensionDate;
                    subscriptionInfo.Subscription.UnpaidPeriodEndDate = unpaidPeriodEndDate;
                }
            }
        }

        if (sub is { CanceledAt: not null } || string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
        {
            return subscriptionInfo;
        }

        try
        {
            var upcomingInvoiceOptions = new UpcomingInvoiceOptions { Customer = subscriber.GatewayCustomerId };
            var upcomingInvoice = await _stripeAdapter.InvoiceUpcomingAsync(upcomingInvoiceOptions);

            if (upcomingInvoice != null)
            {
                subscriptionInfo.UpcomingInvoice = new SubscriptionInfo.BillingUpcomingInvoice(upcomingInvoice);
            }
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Encountered an unexpected Stripe error");
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
            new CustomerGetOptions { Expand = ["tax_ids"] });

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
            var customer = await _stripeAdapter.CustomerUpdateAsync(subscriber.GatewayCustomerId, new CustomerUpdateOptions
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
                Expand = ["tax_ids"]
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
                    await _stripeAdapter.TaxIdCreateAsync(customer.Id, new TaxIdCreateOptions
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
                new TaxRateUpdateOptions() { Active = false }
        );
        if (!updatedStripeTaxRate.Active)
        {
            taxRate.Active = false;
            await _taxRateRepository.ArchiveAsync(taxRate);
        }
    }

    public async Task<string> AddSecretsManagerToSubscription(
        Organization org,
        StaticStore.Plan plan,
        int additionalSmSeats,
        int additionalServiceAccount) =>
        await FinalizeSubscriptionChangeAsync(
            org,
            new SecretsManagerSubscribeUpdate(org, plan, additionalSmSeats, additionalServiceAccount),
            true);

    public async Task<bool> RisksSubscriptionFailure(Organization organization)
    {
        var subscriptionInfo = await GetSubscriptionAsync(organization);

        if (subscriptionInfo.Subscription is not
            {
                Status: "active" or "trialing" or "past_due",
                CollectionMethod: "charge_automatically"
            }
            || subscriptionInfo.UpcomingInvoice == null)
        {
            return false;
        }

        var customer = await GetCustomerAsync(organization.GatewayCustomerId, GetCustomerPaymentOptions());

        var paymentSource = await GetBillingPaymentSourceAsync(customer);

        return paymentSource == null;
    }

    public async Task<bool> HasSecretsManagerStandalone(Organization organization)
    {
        if (string.IsNullOrEmpty(organization.GatewayCustomerId))
        {
            return false;
        }

        var customer = await _stripeAdapter.CustomerGetAsync(organization.GatewayCustomerId);

        return customer?.Discount?.Coupon?.Id == SecretsManagerStandaloneDiscountId;
    }

    public async Task<(DateTime?, DateTime?)> GetSuspensionDateAsync(Subscription subscription)
    {
        if (subscription.Status is not "past_due" && subscription.Status is not "unpaid")
        {
            return (null, null);
        }

        var openInvoices = await _stripeAdapter.InvoiceSearchAsync(new InvoiceSearchOptions
        {
            Query = $"subscription:'{subscription.Id}' status:'open'"
        });

        if (openInvoices.Count == 0)
        {
            return (null, null);
        }

        var currentDate = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;

        switch (subscription.CollectionMethod)
        {
            case "charge_automatically":
                {
                    var firstOverdueInvoice = openInvoices
                        .Where(invoice => invoice.PeriodEnd < currentDate && invoice.Attempted)
                        .MinBy(invoice => invoice.Created);

                    return (firstOverdueInvoice?.Created.AddDays(14), firstOverdueInvoice?.PeriodEnd);
                }
            case "send_invoice":
                {
                    var firstOverdueInvoice = openInvoices
                        .Where(invoice => invoice.DueDate < currentDate)
                        .MinBy(invoice => invoice.Created);

                    return (firstOverdueInvoice?.DueDate?.AddDays(30), firstOverdueInvoice?.PeriodEnd);
                }
            default: return (null, null);
        }
    }

    private PaymentMethod GetLatestCardPaymentMethod(string customerId)
    {
        var cardPaymentMethods = _stripeAdapter.PaymentMethodListAutoPaging(
            new PaymentMethodListOptions { Customer = customerId, Type = "card" });
        return cardPaymentMethods.OrderByDescending(m => m.Created).FirstOrDefault();
    }

    private decimal GetBillingBalance(Customer customer)
    {
        return customer != null ? customer.Balance / 100M : default;
    }

    private async Task<BillingInfo.BillingSource> GetBillingPaymentSourceAsync(Customer customer)
    {
        if (customer == null)
        {
            return null;
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
            (customer.DefaultSource is Card || customer.DefaultSource is BankAccount))
        {
            return new BillingInfo.BillingSource(customer.DefaultSource);
        }

        var paymentMethod = GetLatestCardPaymentMethod(customer.Id);
        return paymentMethod != null ? new BillingInfo.BillingSource(paymentMethod) : null;
    }

    private CustomerGetOptions GetCustomerPaymentOptions()
    {
        var customerOptions = new CustomerGetOptions();
        customerOptions.AddExpand("default_source");
        customerOptions.AddExpand("invoice_settings.default_payment_method");
        return customerOptions;
    }

    private async Task<Customer> GetCustomerAsync(string gatewayCustomerId, CustomerGetOptions options = null)
    {
        if (string.IsNullOrWhiteSpace(gatewayCustomerId))
        {
            return null;
        }

        Customer customer = null;
        try
        {
            customer = await _stripeAdapter.CustomerGetAsync(gatewayCustomerId, options);
        }
        catch (StripeException) { }

        return customer;
    }

    private async Task<IEnumerable<BillingHistoryInfo.BillingTransaction>> GetBillingTransactionsAsync(ISubscriber subscriber, int? limit = null)
    {
        var transactions = subscriber switch
        {
            User => await _transactionRepository.GetManyByUserIdAsync(subscriber.Id, limit),
            Organization => await _transactionRepository.GetManyByOrganizationIdAsync(subscriber.Id, limit),
            _ => null
        };

        return transactions?.OrderByDescending(i => i.CreationDate)
            .Select(t => new BillingHistoryInfo.BillingTransaction(t));
    }

    private async Task<IEnumerable<BillingHistoryInfo.BillingInvoice>> GetBillingInvoicesAsync(Customer customer,
        int? limit = null)
    {
        if (customer == null)
        {
            return null;
        }

        try
        {
            var paidInvoicesTask = _stripeAdapter.InvoiceListAsync(new StripeInvoiceListOptions
            {
                Customer = customer.Id,
                SelectAll = !limit.HasValue,
                Limit = limit,
                Status = "paid"
            });
            var openInvoicesTask = _stripeAdapter.InvoiceListAsync(new StripeInvoiceListOptions
            {
                Customer = customer.Id,
                SelectAll = !limit.HasValue,
                Limit = limit,
                Status = "open"
            });
            var uncollectibleInvoicesTask = _stripeAdapter.InvoiceListAsync(new StripeInvoiceListOptions
            {
                Customer = customer.Id,
                SelectAll = !limit.HasValue,
                Limit = limit,
                Status = "uncollectible"
            });

            var paidInvoices = await paidInvoicesTask;
            var openInvoices = await openInvoicesTask;
            var uncollectibleInvoices = await uncollectibleInvoicesTask;

            var invoices = paidInvoices
                .Concat(openInvoices)
                .Concat(uncollectibleInvoices);

            var result = invoices
                .OrderByDescending(invoice => invoice.Created)
                .Select(invoice => new BillingHistoryInfo.BillingInvoice(invoice));

            return limit.HasValue
                ? result.Take(limit.Value)
                : result;
        }
        catch (StripeException exception)
        {
            _logger.LogError(exception, "An error occurred while listing Stripe invoices");
            throw new GatewayException("Failed to retrieve current invoices", exception);
        }
    }

    /// <summary>
    /// Determines if a Stripe customer supports automatic tax
    /// </summary>
    /// <param name="customer"></param>
    /// <returns></returns>
    private static bool CustomerHasTaxLocationVerified(Customer customer) =>
        customer?.Tax?.AutomaticTax == StripeConstants.AutomaticTaxStatus.Supported;

    // We are taking only first 30 characters of the SubscriberName because stripe provide
    // for 30 characters  for custom_fields,see the link: https://stripe.com/docs/api/invoices/create
    private static string GetFirstThirtyCharacters(string subscriberName)
    {
        if (string.IsNullOrWhiteSpace(subscriberName))
        {
            return string.Empty;
        }

        return subscriberName.Length <= 30
            ? subscriberName
            : subscriberName[..30];
    }
}
