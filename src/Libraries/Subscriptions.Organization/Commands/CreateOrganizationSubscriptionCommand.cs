using Bit.Core.Billing;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Models.Sales;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Tax.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Braintree;
using Microsoft.Extensions.Logging;
using Stripe;
using Customer = Stripe.Customer;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;
using Plan = Bit.Core.Models.StaticStore.Plan;
using StripeConstants = Bit.Core.Billing.Constants.StripeConstants;
using Subscription = Stripe.Subscription;

namespace Bit.Subscriptions.Organization.Commands;

internal sealed class CreateOrganizationSubscriptionCommand(
    IBraintreeGateway braintreeGateway,
    ILogger<CreateOrganizationSubscriptionCommand> logger,
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter,
    ISubscriberService subscriberService,
    ISubscriptionDiscountService subscriptionDiscountService,
    ITaxService taxService) : ICreateOrganizationSubscriptionCommand
{
    // Must match `InitiationPath.SalesAssistedTrialFromAdminPortal` in the clients repo.
    private const string _salesAssistedTrialInitiationPath = "Sales assisted trial from admin portal";

    // Matched as a substring of the client's `InitiationPath` marketing-trial values.
    private const string _marketingTrialInitiationPathSegment = "trial from marketing website";

    public async Task<Subscription> Run(OrganizationEntity organization, OrganizationSubscriptionCreation purchase)
    {
        var plan = await pricingClient.GetPlanOrThrow(purchase.PlanType);

        var customerSetup = purchase.CustomerSetup;

        ArgumentNullException.ThrowIfNull(customerSetup);

        if (customerSetup is { SystemCoupons.Length: > 0, DiscountCoupons.Length: > 0 })
        {
            throw new BadRequestException("A subscription cannot be created with both system and discount coupons.");
        }

        var privilegedControlsSeatsMinimum = ResolvePrivilegedControlSeatsMinimum(plan, purchase);

        var coupons = await ResolveCouponsAsync(customerSetup, purchase.Owner, purchase.PlanType);

        var customer = await CreateCustomerAsync(organization, customerSetup, purchase.CloudRegion);

        var subscription = await CreateSubscriptionAsync(organization, customer, purchase, plan, coupons,
            privilegedControlsSeatsMinimum);

        ApplyResultsToOrganization(organization, customer, subscription, privilegedControlsSeatsMinimum);

        return subscription;
    }

    private async Task<List<string>> ResolveCouponsAsync(CustomerSetup customerSetup, User? owner, PlanType planType)
    {
        var systemCoupons =
            customerSetup
                .SystemCoupons?
                .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList() ?? [];

        if (systemCoupons.Count > 0)
        {
            return systemCoupons;
        }

        // Discount coupons are only allowed for Families organizations.
        if (customerSetup.DiscountCoupons is not { Length: > 0 } ||
            owner == null ||
            planType.GetProductTier() != ProductTierType.Families)
        {
            return [];
        }

        var discountCoupons =
            customerSetup.DiscountCoupons
                .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList();

        if (!await subscriptionDiscountService.ValidateDiscountEligibilityForUserAsync(owner, discountCoupons,
                DiscountTierType.Families))
        {
            throw new BadRequestException("Discount expired. Please review your cart total and try again");
        }

        return discountCoupons;
    }

    private static int? ResolvePrivilegedControlSeatsMinimum(Plan plan, OrganizationSubscriptionCreation purchase)
    {
        if (purchase.PrivilegedControls is null)
        {
            return null;
        }

        if (!plan.SupportsPrivilegedControls)
        {
            throw new BadRequestException("Invalid Privileged Controls plan selected.");
        }

        return purchase.PrivilegedControls.RequestedSeatMinimum is { } minimum &&
               plan.PrivilegedControls.PromotionalSeatMinimums.Contains(minimum)
            ? minimum
            : plan.PrivilegedControls.DefaultSeatMinimum;
    }

    private async Task<Customer> CreateCustomerAsync(OrganizationEntity organization, CustomerSetup customerSetup,
        string cloudRegion)
    {
        var customerCreateOptions = new CustomerCreateOptions
        {
            Description = organization.DisplayName(),
            Email = organization.BillingEmail,
            Expand = ["tax", "tax_ids"],
            InvoiceSettings = new CustomerInvoiceSettingsOptions
            {
                CustomFields =
                [
                    new CustomerInvoiceSettingsCustomFieldOptions
                    {
                        Name = organization.SubscriberType(), Value = organization.GetFormattedInvoiceName()
                    }
                ]
            },
            Metadata = new Dictionary<string, string>
            {
                [StripeConstants.MetadataKeys.OrganizationId] = organization.Id.ToString(),
                [StripeConstants.MetadataKeys.Region] = cloudRegion
            }
        };

        string? braintreeCustomerId = null;
        string? setupIntentId = null;

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

                throw new BadRequestException("A valid payment method is required to create the subscription.");
            }

            if (customerSetup.TaxInformation is not { Country: not null and not "", PostalCode: not null and not "" })
            {
                logger.LogError(
                    "Cannot create customer for organization ({OrganizationID}) without valid tax information",
                    organization.Id);

                throw new BadRequestException("Valid tax information is required to create the subscription.");
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

            if (customerSetup.TaxInformation.TaxId is not null and not "")
            {
                var taxIdType = taxService.GetStripeTaxCode(customerSetup.TaxInformation.Country,
                    customerSetup.TaxInformation.TaxId);

                if (taxIdType == null)
                {
                    logger.LogWarning(
                        "Could not determine tax ID type for organization '{OrganizationID}' in country '{Country}' with client-supplied type '{TaxIdType}'.",
                        organization.Id,
                        customerSetup.TaxInformation.Country,
                        customerSetup.TaxInformation.TaxIdType);

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

            switch (paymentMethodType)
            {
                case PaymentMethodType.BankAccount:
                    {
                        var setupIntent =
                            (await stripeAdapter.ListSetupIntentsAsync(
                                new SetupIntentListOptions { PaymentMethod = paymentMethodToken }))
                            .FirstOrDefault();

                        if (setupIntent == null)
                        {
                            logger.LogError(
                                "Cannot create customer for organization ({OrganizationID}) without a setup intent for their bank account",
                                organization.Id);

                            throw new BadRequestException("A setup intent is required for the provided bank account.");
                        }

                        setupIntentId = setupIntent.Id;
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
                        braintreeCustomerId =
                            await subscriberService.CreateBraintreeCustomer(organization, paymentMethodToken);
                        customerCreateOptions.Metadata[Utilities.BraintreeCustomerIdKey] = braintreeCustomerId;
                        break;
                    }
            }
        }

        try
        {
            var customer = await stripeAdapter.CreateCustomerAsync(customerCreateOptions);

            if (setupIntentId is not null)
            {
                await stripeAdapter.UpdateSetupIntentAsync(setupIntentId,
                    new SetupIntentUpdateOptions { Customer = customer.Id });
            }

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
            if (braintreeCustomerId is not null)
            {
                await braintreeGateway.Customer.DeleteAsync(braintreeCustomerId);
            }
        }
    }

    private async Task<Subscription> CreateSubscriptionAsync(
        OrganizationEntity organization,
        Customer customer,
        OrganizationSubscriptionCreation purchase,
        Plan plan,
        IReadOnlyList<string> coupons,
        int? privilegedControlsSeatsMinimum)
    {
        var subscriptionCreateOptions = BuildSubscriptionCreateOptions(organization, customer, purchase, plan,
            coupons, privilegedControlsSeatsMinimum);

        return await stripeAdapter.CreateSubscriptionAsync(subscriptionCreateOptions);
    }

    private static SubscriptionCreateOptions BuildSubscriptionCreateOptions(
        OrganizationEntity organization,
        Customer customer,
        OrganizationSubscriptionCreation purchase,
        Plan plan,
        IReadOnlyList<string> coupons,
        int? privilegedControlsSeatsMinimum)
    {
        var passwordManager = purchase.PasswordManager;

        var subscriptionItemOptionsList = new List<SubscriptionItemOptions>
        {
            plan.HasNonSeatBasedPasswordManagerPlan()
                ? new SubscriptionItemOptions { Price = plan.PasswordManager.StripePlanId, Quantity = 1 }
                : new SubscriptionItemOptions
                {
                    Price = plan.PasswordManager.StripeSeatPlanId, Quantity = passwordManager.Seats
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

        if (passwordManager.Storage is > 0)
        {
            subscriptionItemOptionsList.Add(new SubscriptionItemOptions
            {
                Price = plan.PasswordManager.StripeStoragePlanId,
                Quantity = passwordManager.Storage
            });
        }

        if (purchase.SecretsManager is { } secretsManager)
        {
            subscriptionItemOptionsList.Add(new SubscriptionItemOptions
            {
                Price = plan.SecretsManager.StripeSeatPlanId,
                Quantity = secretsManager.Seats
            });

            if (secretsManager.ServiceAccounts is > 0)
            {
                subscriptionItemOptionsList.Add(new SubscriptionItemOptions
                {
                    Price = plan.SecretsManager.StripeServiceAccountPlanId,
                    Quantity = secretsManager.ServiceAccounts
                });
            }
        }

        if (privilegedControlsSeatsMinimum is { } pamSeats)
        {
            subscriptionItemOptionsList.Add(new SubscriptionItemOptions
            {
                Price = plan.PrivilegedControls.StripeSeatPlanId,
                Quantity = pamSeats
            });
        }

        var subscriptionCreateOptions = new SubscriptionCreateOptions
        {
            BillingMode = new SubscriptionBillingModeOptions { Type = StripeConstants.BillingMode.Classic },
            CollectionMethod = StripeConstants.CollectionMethod.ChargeAutomatically,
            Customer = customer.Id,
            Items = subscriptionItemOptionsList,
            Metadata = new Dictionary<string, string>
            {
                [StripeConstants.MetadataKeys.OrganizationId] = organization.Id.ToString(),
                [StripeConstants.MetadataKeys.TrialInitiationPath] = purchase.Trial.InitiationPath switch
                {
                    var path when !string.IsNullOrEmpty(path) && path.Contains(_marketingTrialInitiationPathSegment)
                        => StripeConstants.TrialInitiationPaths.MarketingInitiated,
                    _salesAssistedTrialInitiationPath
                        => StripeConstants.TrialInitiationPaths.SalesAssisted,
                    _ => StripeConstants.TrialInitiationPaths.ProductInitiated
                }
            },
            OffSession = true,
            TrialPeriodDays = purchase.Trial.Skip ? 0 : purchase.Trial.Length ?? plan.TrialPeriodDays
        };

        if (coupons.Count > 0)
        {
            subscriptionCreateOptions.Discounts = coupons
                .Select(c => new SubscriptionDiscountOptions { Coupon = c })
                .ToList();
        }

        // This command is only used for creating new subscriptions, so we can assume that the subscription is being created for the first time.
        // The attached method would be the first payment method for the customer, so an additional query is not required.
        var hasPaymentMethod = purchase.CustomerSetup!.IsBillable;

        // Only set trial_settings.end_behavior.missing_payment_method to "cancel"
        // if there is no payment method AND there's an actual trial period.
        if (!hasPaymentMethod && subscriptionCreateOptions.TrialPeriodDays > 0)
        {
            subscriptionCreateOptions.TrialSettings = new SubscriptionTrialSettingsOptions
            {
                EndBehavior = new SubscriptionTrialSettingsEndBehaviorOptions { MissingPaymentMethod = "cancel" }
            };
        }

        if (customer.HasBillingLocation())
        {
            subscriptionCreateOptions.AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true };
        }

        return subscriptionCreateOptions;
    }

    private static void ApplyResultsToOrganization(
        OrganizationEntity organization,
        Customer customer,
        Subscription subscription,
        int? privilegedControlsSeatsMinimum)
    {
        organization.Gateway = GatewayType.Stripe;
        organization.GatewayCustomerId = customer.Id;
        organization.GatewaySubscriptionId = subscription.Id;

        if (subscription.Status is StripeConstants.SubscriptionStatus.Trialing
            or StripeConstants.SubscriptionStatus.Active)
        {
            organization.Enabled = true;
            organization.ExpirationDate = subscription.GetCurrentPeriodEnd();
        }

        if (privilegedControlsSeatsMinimum is { } pamSeats)
        {
            organization.PamSeats = pamSeats;
            organization.MaxAutoscalePamSeats = null;
            organization.UsePam = true;
            organization.PamSeatMinimum = pamSeats;
        }
    }
}
