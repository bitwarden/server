using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Braintree;
using Microsoft.Extensions.Logging;
using Stripe;

using static Bit.Core.Billing.Utilities;
using Customer = Stripe.Customer;
using Subscription = Stripe.Subscription;

namespace Bit.Core.Billing.Services.Implementations;

public class OrganizationBillingService(
    IBraintreeGateway braintreeGateway,
    IGlobalSettings globalSettings,
    ILogger<OrganizationBillingService> logger,
    IOrganizationRepository organizationRepository,
    ISetupIntentCache setupIntentCache,
    IStripeAdapter stripeAdapter,
    ISubscriberService subscriberService) : IOrganizationBillingService
{
    public async Task<OrganizationMetadata> GetMetadata(Guid organizationId)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return null;
        }

        var customer = await subscriberService.GetCustomer(organization, new CustomerGetOptions
        {
            Expand = ["discount.coupon.applies_to"]
        });

        var subscription = await subscriberService.GetSubscription(organization);

        if (customer == null || subscription == null)
        {
            return OrganizationMetadata.Default();
        }

        var isOnSecretsManagerStandalone = IsOnSecretsManagerStandalone(organization, customer, subscription);

        return new OrganizationMetadata(isOnSecretsManagerStandalone);
    }

    public async Task PurchaseSubscription(
        Organization organization,
        OrganizationSubscriptionPurchase organizationSubscriptionPurchase)
    {
        ArgumentNullException.ThrowIfNull(organization);
        ArgumentNullException.ThrowIfNull(organizationSubscriptionPurchase);

        var (
            metadata,
            passwordManager,
            paymentSource,
            planType,
            secretsManager,
            taxInformation) = organizationSubscriptionPurchase;

        var customer = await CreateCustomerAsync(organization, metadata, paymentSource, taxInformation);

        var subscription =
            await CreateSubscriptionAsync(customer, organization.Id, passwordManager, planType, secretsManager);

        organization.Enabled = true;
        organization.ExpirationDate = subscription.CurrentPeriodEnd;
        organization.Gateway = GatewayType.Stripe;
        organization.GatewayCustomerId = customer.Id;
        organization.GatewaySubscriptionId = subscription.Id;

        await organizationRepository.ReplaceAsync(organization);
    }

    #region Utilities

    private async Task<Customer> CreateCustomerAsync(
        Organization organization,
        OrganizationSubscriptionPurchaseMetadata metadata,
        TokenizedPaymentSource paymentSource,
        TaxInformation taxInformation)
    {
        if (paymentSource == null)
        {
            logger.LogError(
                "Cannot create customer for organization ({OrganizationID}) without a payment source",
                organization.Id);

            throw new BillingException();
        }

        if (taxInformation is not { Country: not null, PostalCode: not null })
        {
            logger.LogError(
                "Cannot create customer for organization ({OrganizationID}) without both a country and postal code",
                organization.Id);

            throw new BillingException();
        }

        var (
            country,
            postalCode,
            taxId,
            line1,
            line2,
            city,
            state) = taxInformation;

        var address = new AddressOptions
        {
            Country = country,
            PostalCode = postalCode,
            City = city,
            Line1 = line1,
            Line2 = line2,
            State = state
        };

        var (fromProvider, fromSecretsManagerStandalone) = metadata ?? OrganizationSubscriptionPurchaseMetadata.Default;

        var coupon = fromProvider
            ? StripeConstants.CouponIDs.MSPDiscount35
            : fromSecretsManagerStandalone
                ? StripeConstants.CouponIDs.SecretsManagerStandalone
                : null;

        var organizationDisplayName = organization.DisplayName();

        var customerCreateOptions = new CustomerCreateOptions
        {
            Address = address,
            Coupon = coupon,
            Description = organization.DisplayBusinessName(),
            Email = organization.BillingEmail,
            Expand = ["tax"],
            InvoiceSettings = new CustomerInvoiceSettingsOptions
            {
                CustomFields = [
                    new CustomerInvoiceSettingsCustomFieldOptions
                    {
                        Name = organization.SubscriberType(),
                        Value = organizationDisplayName.Length <= 30
                            ? organizationDisplayName
                            : organizationDisplayName[..30]
                    }]
            },
            Metadata = new Dictionary<string, string>
            {
                { "region", globalSettings.BaseServiceUri.CloudRegion }
            },
            Tax = new CustomerTaxOptions
            {
                ValidateLocation = StripeConstants.ValidateTaxLocationTiming.Immediately
            },
            TaxIdData = !string.IsNullOrEmpty(taxId)
                ? [new CustomerTaxIdDataOptions { Type = taxInformation.GetTaxIdType(), Value = taxId }]
                : null
        };

        var (type, token) = paymentSource;

        if (string.IsNullOrEmpty(token))
        {
            logger.LogError(
                "Cannot create customer for organization ({OrganizationID}) without a payment source token",
                organization.Id);

            throw new BillingException();
        }

        var braintreeCustomerId = "";

        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (type)
        {
            case PaymentMethodType.BankAccount:
                {
                    var setupIntent =
                        (await stripeAdapter.SetupIntentList(new SetupIntentListOptions { PaymentMethod = token }))
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
                    customerCreateOptions.PaymentMethod = token;
                    customerCreateOptions.InvoiceSettings.DefaultPaymentMethod = token;
                    break;
                }
            case PaymentMethodType.PayPal:
                {
                    braintreeCustomerId = await subscriberService.CreateBraintreeCustomer(organization, token);

                    customerCreateOptions.Metadata[BraintreeCustomerIdKey] = braintreeCustomerId;

                    break;
                }
            default:
                {
                    logger.LogError("Cannot create customer for organization ({OrganizationID}) using payment method type ({PaymentMethodType}) as it is not supported", organization.Id, type.ToString());

                    throw new BillingException();
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
            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (type)
            {
                case PaymentMethodType.BankAccount:
                    {
                        await setupIntentCache.Remove(organization.Id);
                        break;
                    }
                case PaymentMethodType.PayPal:
                    {
                        await braintreeGateway.Customer.DeleteAsync(braintreeCustomerId);
                        break;
                    }
            }
        }
    }

    private async Task<Subscription> CreateSubscriptionAsync(
        Customer customer,
        Guid organizationId,
        OrganizationPasswordManagerSubscriptionPurchase passwordManager,
        PlanType planType,
        OrganizationSecretsManagerSubscriptionPurchase secretsManager)
    {
        var plan = StaticStore.GetPlan(planType);

        if (passwordManager == null)
        {
            logger.LogError("Cannot create subscription for organization ({OrganizationID}) without password manager purchase information", organizationId);

            throw new BillingException();
        }

        var subscriptionItemOptionsList = new List<SubscriptionItemOptions>
        {
            new ()
            {
                Price = plan.PasswordManager.StripeSeatPlanId,
                Quantity = passwordManager.Seats
            }
        };

        if (passwordManager.PremiumAccess)
        {
            subscriptionItemOptionsList.Add(new SubscriptionItemOptions
            {
                Price = plan.PasswordManager.StripePremiumAccessPlanId,
                Quantity = 1
            });
        }

        if (passwordManager.Storage > 0)
        {
            subscriptionItemOptionsList.Add(new SubscriptionItemOptions
            {
                Price = plan.PasswordManager.StripeStoragePlanId,
                Quantity = passwordManager.Storage
            });
        }

        if (secretsManager != null)
        {
            subscriptionItemOptionsList.Add(new SubscriptionItemOptions
            {
                Price = plan.SecretsManager.StripeSeatPlanId,
                Quantity = secretsManager.Seats
            });

            if (secretsManager.ServiceAccounts > 0)
            {
                subscriptionItemOptionsList.Add(new SubscriptionItemOptions
                {
                    Price = plan.SecretsManager.StripeServiceAccountPlanId,
                    Quantity = secretsManager.ServiceAccounts
                });
            }
        }

        var subscriptionCreateOptions = new SubscriptionCreateOptions
        {
            AutomaticTax = new SubscriptionAutomaticTaxOptions
            {
                Enabled = customer.Tax?.AutomaticTax == StripeConstants.AutomaticTaxStatus.Supported
            },
            CollectionMethod = StripeConstants.CollectionMethod.ChargeAutomatically,
            Customer = customer.Id,
            Items = subscriptionItemOptionsList,
            Metadata = new Dictionary<string, string>
            {
                ["organizationId"] = organizationId.ToString()
            },
            OffSession = true,
            TrialPeriodDays = plan.TrialPeriodDays,
        };

        try
        {
            return await stripeAdapter.SubscriptionCreateAsync(subscriptionCreateOptions);
        }
        catch
        {
            await stripeAdapter.CustomerDeleteAsync(customer.Id);
            throw;
        }
    }

    private static bool IsOnSecretsManagerStandalone(
        Organization organization,
        Customer customer,
        Subscription subscription)
    {
        var plan = StaticStore.GetPlan(organization.PlanType);

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
