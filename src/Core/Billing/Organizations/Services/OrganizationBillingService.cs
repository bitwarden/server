using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Models.Sales;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Tax.Models;
using Bit.Core.Billing.Tax.Services;
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

public class OrganizationBillingService(
    IBraintreeGateway braintreeGateway,
    IGlobalSettings globalSettings,
    IHasPaymentMethodQuery hasPaymentMethodQuery,
    ILogger<OrganizationBillingService> logger,
    IOrganizationRepository organizationRepository,
    IPricingClient pricingClient,
    ISetupIntentCache setupIntentCache,
    IStripeAdapter stripeAdapter,
    ISubscriberService subscriberService,
    ITaxService taxService) : IOrganizationBillingService
{
    public async Task Finalize(OrganizationSale sale)
    {
        var (organization, customerSetup, subscriptionSetup) = sale;

        var customer = string.IsNullOrEmpty(organization.GatewayCustomerId) && customerSetup != null
            ? await CreateCustomerAsync(organization, customerSetup, subscriptionSetup.PlanType)
            : await GetCustomerWhileEnsuringCorrectTaxExemptionAsync(organization, subscriptionSetup);

        var subscription = await CreateSubscriptionAsync(organization, customer, subscriptionSetup, customerSetup?.Coupon);

        if (subscription.Status is StripeConstants.SubscriptionStatus.Trialing or StripeConstants.SubscriptionStatus.Active)
        {
            organization.Enabled = true;
            organization.ExpirationDate = subscription.GetCurrentPeriodEnd();
            await organizationRepository.ReplaceAsync(organization);
        }
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

        var orgOccupiedSeats = await organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id);

        if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
        {
            return OrganizationMetadata.Default with
            {
                OrganizationOccupiedSeats = orgOccupiedSeats.Total
            };
        }

        var customer = await subscriberService.GetCustomer(organization);

        var subscription = await subscriberService.GetSubscription(organization, new SubscriptionGetOptions
        {
            Expand = ["discounts.coupon.applies_to"]
        });

        if (customer == null || subscription == null)
        {
            return OrganizationMetadata.Default with
            {
                OrganizationOccupiedSeats = orgOccupiedSeats.Total
            };
        }

        var isOnSecretsManagerStandalone = await IsOnSecretsManagerStandalone(organization, customer, subscription);

        return new OrganizationMetadata(
            isOnSecretsManagerStandalone,
            orgOccupiedSeats.Total);
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
            await UpdateMissingPaymentMethodBehaviourAsync(organization);
        }
    }

    public async Task UpdateSubscriptionPlanFrequency(
        Organization organization, PlanType newPlanType)
    {
        ArgumentNullException.ThrowIfNull(organization);

        var subscription = await subscriberService.GetSubscriptionOrThrow(organization);
        var subscriptionItems = subscription.Items.Data;

        var newPlan = await pricingClient.GetPlanOrThrow(newPlanType);
        var oldPlan = await pricingClient.GetPlanOrThrow(organization.PlanType);

        // Build the subscription update options
        var subscriptionItemOptions = new List<SubscriptionItemOptions>();
        foreach (var item in subscriptionItems)
        {
            var subscriptionItemOption = new SubscriptionItemOptions
            {
                Id = item.Id,
                Quantity = item.Quantity,
                Price = item.Price.Id == oldPlan.SecretsManager.StripeSeatPlanId ? newPlan.SecretsManager.StripeSeatPlanId : newPlan.PasswordManager.StripeSeatPlanId
            };

            subscriptionItemOptions.Add(subscriptionItemOption);
        }
        var updateOptions = new SubscriptionUpdateOptions
        {
            Items = subscriptionItemOptions,
            ProrationBehavior = StripeConstants.ProrationBehavior.CreateProrations
        };

        try
        {
            // Update the subscription in Stripe
            await stripeAdapter.SubscriptionUpdateAsync(subscription.Id, updateOptions);
            organization.PlanType = newPlan.Type;
            await organizationRepository.ReplaceAsync(organization);
        }
        catch (StripeException stripeException)
        {
            logger.LogError(stripeException, "Failed to update subscription plan for subscriber ({SubscriberID}): {Error}",
                organization.Id, stripeException.Message);

            throw new BillingException(
                message: "An error occurred while updating the subscription plan",
                innerException: stripeException);
        }
    }

    public async Task UpdateOrganizationNameAndEmail(Organization organization)
    {
        if (organization.GatewayCustomerId is null)
        {
            throw new BillingException("Cannot update an organization in Stripe without a GatewayCustomerId.");
        }

        var newDisplayName = organization.DisplayName();

        await stripeAdapter.CustomerUpdateAsync(organization.GatewayCustomerId,
            new CustomerUpdateOptions
            {
                Email = organization.BillingEmail,
                Description = newDisplayName,
                InvoiceSettings = new CustomerInvoiceSettingsOptions
                {
                    // This overwrites the existing custom fields for this organization
                    CustomFields = [
                        new CustomerInvoiceSettingsCustomFieldOptions
                        {
                            Name = organization.SubscriberType(),
                            Value = newDisplayName.Length <= 30
                                ? newDisplayName
                                : newDisplayName[..30]
                        }]
                },
            });
    }

    #region Utilities

    private async Task<Customer> CreateCustomerAsync(
        Organization organization,
        CustomerSetup customerSetup,
        PlanType? updatedPlanType = null)
    {
        var planType = updatedPlanType ?? organization.PlanType;

        var displayName = organization.DisplayName();

        var customerCreateOptions = new CustomerCreateOptions
        {
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
                Country = customerSetup.TaxInformation.Country
            };

            customerCreateOptions.Tax = new CustomerTaxOptions
            {
                ValidateLocation = StripeConstants.ValidateTaxLocationTiming.Immediately
            };

            if (planType.GetProductTier() is not ProductTierType.Free and not ProductTierType.Families &&
                customerSetup.TaxInformation.Country != Core.Constants.CountryAbbreviations.UnitedStates)
            {
                customerCreateOptions.TaxExempt = StripeConstants.TaxExempt.Reverse;
            }

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

                    throw new BadRequestException("billingTaxIdTypeInferenceError");
                }

                customerCreateOptions.TaxIdData =
                [
                    new CustomerTaxIdDataOptions { Type = taxIdType, Value = customerSetup.TaxInformation.TaxId }
                ];

                if (taxIdType == StripeConstants.TaxIdType.SpanishNIF)
                {
                    customerCreateOptions.TaxIdData.Add(new CustomerTaxIdDataOptions
                    {
                        Type = StripeConstants.TaxIdType.EUVAT,
                        Value = $"ES{customerSetup.TaxInformation.TaxId}"
                    });
                }
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
            var customer = await stripeAdapter.CustomerCreateAsync(customerCreateOptions);

            organization.Gateway = GatewayType.Stripe;
            organization.GatewayCustomerId = customer.Id;
            await organizationRepository.ReplaceAsync(organization);

            return customer;
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
                            await setupIntentCache.RemoveSetupIntentForSubscriber(organization.Id);
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
        Organization organization,
        Customer customer,
        SubscriptionSetup subscriptionSetup,
        string? coupon)
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
            Discounts = !string.IsNullOrEmpty(coupon) ? [new SubscriptionDiscountOptions { Coupon = coupon }] : null,
            Items = subscriptionItemOptionsList,
            Metadata = new Dictionary<string, string>
            {
                ["organizationId"] = organization.Id.ToString(),
                ["trialInitiationPath"] = !string.IsNullOrEmpty(subscriptionSetup.InitiationPath) &&
                    subscriptionSetup.InitiationPath.Contains("trial from marketing website")
                    ? "marketing-initiated"
                    : "product-initiated"
            },
            OffSession = true,
            TrialPeriodDays = subscriptionSetup.SkipTrial ? 0 : plan.TrialPeriodDays
        };

        var hasPaymentMethod = await hasPaymentMethodQuery.Run(organization);

        // Only set trial_settings.end_behavior.missing_payment_method to "cancel"
        // if there is no payment method AND there's an actual trial period
        if (!hasPaymentMethod && subscriptionCreateOptions.TrialPeriodDays > 0)
        {
            subscriptionCreateOptions.TrialSettings = new SubscriptionTrialSettingsOptions
            {
                EndBehavior = new SubscriptionTrialSettingsEndBehaviorOptions
                {
                    MissingPaymentMethod = "cancel"
                }
            };
        }

        if (customer.HasBillingLocation())
        {
            subscriptionCreateOptions.AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true };
        }

        var subscription = await stripeAdapter.SubscriptionCreateAsync(subscriptionCreateOptions);

        organization.GatewaySubscriptionId = subscription.Id;
        await organizationRepository.ReplaceAsync(organization);

        return subscription;
    }

    private async Task<Customer> GetCustomerWhileEnsuringCorrectTaxExemptionAsync(
        Organization organization,
        SubscriptionSetup subscriptionSetup)
    {
        var customer = await subscriberService.GetCustomerOrThrow(organization,
            new CustomerGetOptions { Expand = ["tax", "tax_ids"] });

        if (subscriptionSetup.PlanType.GetProductTier() is
                not (ProductTierType.Teams or
                ProductTierType.TeamsStarter or
                ProductTierType.Enterprise))
        {
            return customer;
        }

        List<string> expansions = ["tax", "tax_ids"];

        customer = customer switch
        {
            { Address.Country: not Core.Constants.CountryAbbreviations.UnitedStates, TaxExempt: not StripeConstants.TaxExempt.Reverse } => await
                stripeAdapter.CustomerUpdateAsync(customer.Id,
                    new CustomerUpdateOptions
                    {
                        Expand = expansions,
                        TaxExempt = StripeConstants.TaxExempt.Reverse
                    }),
            { Address.Country: Core.Constants.CountryAbbreviations.UnitedStates, TaxExempt: StripeConstants.TaxExempt.Reverse } => await
                stripeAdapter.CustomerUpdateAsync(customer.Id,
                    new CustomerUpdateOptions
                    {
                        Expand = expansions,
                        TaxExempt = StripeConstants.TaxExempt.None
                    }),
            _ => customer
        };

        return customer;
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

        var coupon = subscription.Discounts?.FirstOrDefault(discount =>
            discount.Coupon?.Id == StripeConstants.CouponIDs.SecretsManagerStandalone)?.Coupon;

        if (coupon == null)
        {
            return false;
        }

        var subscriptionProductIds = subscription.Items.Data.Select(item => item.Plan.ProductId);

        var couponAppliesTo = coupon.AppliesTo?.Products;

        return subscriptionProductIds.Intersect(couponAppliesTo ?? []).Any();
    }

    private async Task UpdateMissingPaymentMethodBehaviourAsync(Organization organization)
    {
        var subscription = await subscriberService.GetSubscriptionOrThrow(organization);
        if (subscription.TrialSettings?.EndBehavior?.MissingPaymentMethod == StripeConstants.MissingPaymentMethodBehaviorOptions.Cancel)
        {
            var options = new SubscriptionUpdateOptions
            {
                TrialSettings = new SubscriptionTrialSettingsOptions
                {
                    EndBehavior = new SubscriptionTrialSettingsEndBehaviorOptions
                    {
                        MissingPaymentMethod = StripeConstants.MissingPaymentMethodBehaviorOptions.CreateInvoice
                    }
                }
            };
            await stripeAdapter.SubscriptionUpdateAsync(organization.GatewaySubscriptionId, options);
        }
    }

    #endregion
}
