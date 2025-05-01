using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Models.Sales;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Payment.Services;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Services.Contracts;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Braintree;
using Microsoft.Extensions.Logging;
using Stripe;
using static Bit.Core.Billing.Utilities;
using Customer = Stripe.Customer;
using Subscription = Stripe.Subscription;

namespace Bit.Core.Billing.Organizations.Services;

#nullable enable

public class OrganizationBillingService(
    IBraintreeGateway braintreeGateway,
    IFeatureService featureService,
    IGlobalSettings globalSettings,
    ILogger<OrganizationBillingService> logger,
    IOrganizationRepository organizationRepository,
    IPricingClient pricingClient,
    ISetupIntentCache setupIntentCache,
    IStripeAdapter stripeAdapter,
    ISubscriberService subscriberService,
    ITaxService taxService,
    IAutomaticTaxFactory automaticTaxFactory) : IOrganizationBillingService
{
    public async Task Finalize(OrganizationSale sale)
    {
        var (organization, customerSetup, subscriptionSetup) = sale;

        var customer = string.IsNullOrEmpty(organization.GatewayCustomerId) && customerSetup != null
            ? await CreateCustomerAsync(organization, customerSetup)
            : await subscriberService.GetCustomerOrThrow(organization, new CustomerGetOptions { Expand = ["tax", "tax_ids"] });

        var subscription = await CreateSubscriptionAsync(organization.Id, customer, subscriptionSetup);

        if (subscription.Status is StripeConstants.SubscriptionStatus.Trialing or StripeConstants.SubscriptionStatus.Active)
        {
            organization.Enabled = true;
            organization.ExpirationDate = subscription.CurrentPeriodEnd;
        }

        organization.Gateway = GatewayType.Stripe;
        organization.GatewayCustomerId = customer.Id;
        organization.GatewaySubscriptionId = subscription.Id;

        await organizationRepository.ReplaceAsync(organization);
    }

    public async Task<OrganizationMetadata?> GetMetadata(Guid organizationId)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return null;
        }

        if (globalSettings.SelfHosted)
        {
            return OrganizationMetadata.Default;
        }

        var isEligibleForSelfHost = await IsEligibleForSelfHostAsync(organization);

        var isManaged = organization.Status == OrganizationStatusType.Managed;

        if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
        {
            return OrganizationMetadata.Default with
            {
                IsEligibleForSelfHost = isEligibleForSelfHost,
                IsManaged = isManaged
            };
        }

        var customer = await subscriberService.GetCustomer(organization,
            new CustomerGetOptions { Expand = ["discount.coupon.applies_to"] });

        var subscription = await subscriberService.GetSubscription(organization);

        if (customer == null || subscription == null)
        {
            return OrganizationMetadata.Default with
            {
                IsEligibleForSelfHost = isEligibleForSelfHost,
                IsManaged = isManaged
            };
        }

        var isOnSecretsManagerStandalone = await IsOnSecretsManagerStandalone(organization, customer, subscription);

        var invoice = !string.IsNullOrEmpty(subscription.LatestInvoiceId)
            ? await stripeAdapter.InvoiceGetAsync(subscription.LatestInvoiceId, new InvoiceGetOptions())
            : null;

        return new OrganizationMetadata(
            isEligibleForSelfHost,
            isManaged,
            isOnSecretsManagerStandalone,
            subscription.Status == StripeConstants.SubscriptionStatus.Unpaid,
            true,
            invoice?.Status == StripeConstants.InvoiceStatus.Open,
            subscription.Status == StripeConstants.SubscriptionStatus.Canceled,
            invoice?.DueDate,
            invoice?.Created,
            subscription.CurrentPeriodEnd);
    }

    public async Task UpdatePaymentMethod(
        Organization organization,
        TokenizedPaymentSource tokenizedPaymentSource,
        TaxInformation taxInformation)
    {
        if (string.IsNullOrEmpty(organization.GatewayCustomerId))
        {
            var customer = await CreateCustomerAsync(organization,
                new CustomerSetup
                {
                    TokenizedPaymentSource = tokenizedPaymentSource,
                    TaxInformation = taxInformation
                });

            organization.Gateway = GatewayType.Stripe;
            organization.GatewayCustomerId = customer.Id;

            await organizationRepository.ReplaceAsync(organization);
        }
        else
        {
            await subscriberService.UpdatePaymentSource(organization, tokenizedPaymentSource);
            await subscriberService.UpdateTaxInformation(organization, taxInformation);
        }
    }

    #region Utilities

    private async Task<Customer> CreateCustomerAsync(
        Organization organization,
        CustomerSetup customerSetup)
    {
        var displayName = organization.DisplayName();

        var customerCreateOptions = new CustomerCreateOptions
        {
            Coupon = customerSetup.Coupon,
            Description = organization.DisplayBusinessName(),
            Email = organization.BillingEmail,
            Expand = ["tax", "tax_ids"],
            InvoiceSettings = new CustomerInvoiceSettingsOptions
            {
                CustomFields = [
                    new CustomerInvoiceSettingsCustomFieldOptions
                    {
                        Name = organization.SubscriberType(),
                        Value = displayName.Length <= 30
                            ? displayName
                            : displayName[..30]
                    }]
            },
            Metadata = new Dictionary<string, string>
            {
                ["organizationId"] = organization.Id.ToString(),
                ["region"] = globalSettings.BaseServiceUri.CloudRegion
            }
        };

        var braintreeCustomerId = "";

        if (customerSetup.IsBillable)
        {
            if (customerSetup.TokenizedPaymentSource is not
                {
                    Type: PaymentMethodType.BankAccount or PaymentMethodType.Card or PaymentMethodType.PayPal,
                    Token: not null and not ""
                })
            {
                logger.LogError(
                    "Cannot create customer for organization ({OrganizationID}) without a valid payment source",
                    organization.Id);

                throw new BillingException();
            }

            if (customerSetup.TaxInformation is not { Country: not null and not "", PostalCode: not null and not "" })
            {
                logger.LogError(
                    "Cannot create customer for organization ({OrganizationID}) without valid tax information",
                    organization.Id);

                throw new BillingException();
            }

            customerCreateOptions.Address = new AddressOptions
            {
                Line1 = customerSetup.TaxInformation.Line1,
                Line2 = customerSetup.TaxInformation.Line2,
                City = customerSetup.TaxInformation.City,
                PostalCode = customerSetup.TaxInformation.PostalCode,
                State = customerSetup.TaxInformation.State,
                Country = customerSetup.TaxInformation.Country,
            };
            customerCreateOptions.Tax = new CustomerTaxOptions
            {
                ValidateLocation = StripeConstants.ValidateTaxLocationTiming.Immediately
            };

            if (!string.IsNullOrEmpty(customerSetup.TaxInformation.TaxId))
            {
                var taxIdType = taxService.GetStripeTaxCode(customerSetup.TaxInformation.Country,
                    customerSetup.TaxInformation.TaxId);

                if (taxIdType == null)
                {
                    logger.LogWarning("Could not determine tax ID type for organization '{OrganizationID}' in country '{Country}' with tax ID '{TaxID}'.",
                        organization.Id,
                        customerSetup.TaxInformation.Country,
                        customerSetup.TaxInformation.TaxId);
                }

                customerCreateOptions.TaxIdData =
                [
                    new() { Type = taxIdType, Value = customerSetup.TaxInformation.TaxId }
                ];
            }

            var (paymentMethodType, paymentMethodToken) = customerSetup.TokenizedPaymentSource;

            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (paymentMethodType)
            {
                case PaymentMethodType.BankAccount:
                    {
                        var setupIntent =
                            (await stripeAdapter.SetupIntentList(new SetupIntentListOptions { PaymentMethod = paymentMethodToken }))
                            .FirstOrDefault();

                        if (setupIntent == null)
                        {
                            logger.LogError("Cannot create customer for organization ({OrganizationID}) without a setup intent for their bank account", organization.Id);
                            throw new BillingException();
                        }

                        await setupIntentCache.Set(organization.Id, setupIntent.Id);
                        break;
                    }
                case PaymentMethodType.Card:
                    {
                        customerCreateOptions.PaymentMethod = paymentMethodToken;
                        customerCreateOptions.InvoiceSettings.DefaultPaymentMethod = paymentMethodToken;
                        break;
                    }
                case PaymentMethodType.PayPal:
                    {
                        braintreeCustomerId = await subscriberService.CreateBraintreeCustomer(organization, paymentMethodToken);
                        customerCreateOptions.Metadata[BraintreeCustomerIdKey] = braintreeCustomerId;
                        break;
                    }
                default:
                    {
                        logger.LogError("Cannot create customer for organization ({OrganizationID}) using payment method type ({PaymentMethodType}) as it is not supported", organization.Id, paymentMethodType.ToString());
                        throw new BillingException();
                    }
            }
        }

        try
        {
            return await stripeAdapter.CustomerCreateAsync(customerCreateOptions);
        }
        catch (StripeException stripeException) when (stripeException.StripeError?.Code ==
                                                      StripeConstants.ErrorCodes.CustomerTaxLocationInvalid)
        {
            await Revert();
            throw new BadRequestException(
                "Your location wasn't recognized. Please ensure your country and postal code are valid.");
        }
        catch (StripeException stripeException) when (stripeException.StripeError?.Code ==
                                                      StripeConstants.ErrorCodes.TaxIdInvalid)
        {
            await Revert();
            throw new BadRequestException(
                "Your tax ID wasn't recognized for your selected country. Please ensure your country and tax ID are valid.");
        }
        catch
        {
            await Revert();
            throw;
        }

        async Task Revert()
        {
            if (customerSetup.IsBillable)
            {
                // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
                switch (customerSetup.TokenizedPaymentSource!.Type)
                {
                    case PaymentMethodType.BankAccount:
                        {
                            await setupIntentCache.Remove(organization.Id);
                            break;
                        }
                    case PaymentMethodType.PayPal when !string.IsNullOrEmpty(braintreeCustomerId):
                        {
                            await braintreeGateway.Customer.DeleteAsync(braintreeCustomerId);
                            break;
                        }
                }
            }
        }
    }

    private async Task<Subscription> CreateSubscriptionAsync(
        Guid organizationId,
        Customer customer,
        SubscriptionSetup subscriptionSetup)
    {
        var plan = await pricingClient.GetPlanOrThrow(subscriptionSetup.PlanType);

        var passwordManagerOptions = subscriptionSetup.PasswordManagerOptions;

        var subscriptionItemOptionsList = new List<SubscriptionItemOptions>
        {
            plan.HasNonSeatBasedPasswordManagerPlan()
                ? new SubscriptionItemOptions
                {
                    Price = plan.PasswordManager.StripePlanId,
                    Quantity = 1
                }
                : new SubscriptionItemOptions
                {
                    Price = plan.PasswordManager.StripeSeatPlanId,
                    Quantity = passwordManagerOptions.Seats
                }
        };

        if (passwordManagerOptions.PremiumAccess is true)
        {
            subscriptionItemOptionsList.Add(new SubscriptionItemOptions
            {
                Price = plan.PasswordManager.StripePremiumAccessPlanId,
                Quantity = 1
            });
        }

        if (passwordManagerOptions.Storage is > 0)
        {
            subscriptionItemOptionsList.Add(new SubscriptionItemOptions
            {
                Price = plan.PasswordManager.StripeStoragePlanId,
                Quantity = passwordManagerOptions.Storage
            });
        }

        var secretsManagerOptions = subscriptionSetup.SecretsManagerOptions;

        if (secretsManagerOptions != null)
        {
            subscriptionItemOptionsList.Add(new SubscriptionItemOptions
            {
                Price = plan.SecretsManager.StripeSeatPlanId,
                Quantity = secretsManagerOptions.Seats
            });

            if (secretsManagerOptions.ServiceAccounts is > 0)
            {
                subscriptionItemOptionsList.Add(new SubscriptionItemOptions
                {
                    Price = plan.SecretsManager.StripeServiceAccountPlanId,
                    Quantity = secretsManagerOptions.ServiceAccounts
                });
            }
        }

        var subscriptionCreateOptions = new SubscriptionCreateOptions
        {
            CollectionMethod = StripeConstants.CollectionMethod.ChargeAutomatically,
            Customer = customer.Id,
            Items = subscriptionItemOptionsList,
            Metadata = new Dictionary<string, string>
            {
                ["organizationId"] = organizationId.ToString()
            },
            OffSession = true,
            TrialPeriodDays = subscriptionSetup.SkipTrial ? 0 : plan.TrialPeriodDays
        };

        if (featureService.IsEnabled(FeatureFlagKeys.PM19147_AutomaticTaxImprovements))
        {
            var automaticTaxParameters = new AutomaticTaxFactoryParameters(subscriptionSetup.PlanType);
            var automaticTaxStrategy = await automaticTaxFactory.CreateAsync(automaticTaxParameters);
            automaticTaxStrategy.SetCreateOptions(subscriptionCreateOptions, customer);
        }
        else
        {
            subscriptionCreateOptions.AutomaticTax ??= new SubscriptionAutomaticTaxOptions();
            subscriptionCreateOptions.AutomaticTax.Enabled = customer.HasBillingLocation();
        }

        return await stripeAdapter.SubscriptionCreateAsync(subscriptionCreateOptions);
    }

    private async Task<bool> IsEligibleForSelfHostAsync(
        Organization organization)
    {
        var plans = await pricingClient.ListPlans();

        var eligibleSelfHostPlans = plans.Where(plan => plan.HasSelfHost).Select(plan => plan.Type);

        return eligibleSelfHostPlans.Contains(organization.PlanType);
    }

    private async Task<bool> IsOnSecretsManagerStandalone(
        Organization organization,
        Customer? customer,
        Subscription? subscription)
    {
        if (customer == null || subscription == null)
        {
            return false;
        }

        var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);

        if (!plan.SupportsSecretsManager)
        {
            return false;
        }

        var hasCoupon = customer.Discount?.Coupon?.Id == StripeConstants.CouponIDs.SecretsManagerStandalone;

        if (!hasCoupon)
        {
            return false;
        }

        var subscriptionProductIds = subscription.Items.Data.Select(item => item.Plan.ProductId);

        var couponAppliesTo = customer.Discount?.Coupon?.AppliesTo?.Products;

        return subscriptionProductIds.Intersect(couponAppliesTo ?? []).Any();
    }

    #endregion
}
