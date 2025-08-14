// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Globalization;
using Bit.Commercial.Core.Billing.Providers.Models;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing;
using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Providers.Entities;
using Bit.Core.Billing.Providers.Models;
using Bit.Core.Billing.Providers.Repositories;
using Bit.Core.Billing.Providers.Services;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Tax.Models;
using Bit.Core.Billing.Tax.Services;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Braintree;
using CsvHelper;
using Microsoft.Extensions.Logging;
using Stripe;
using static Bit.Core.Billing.Utilities;
using Customer = Stripe.Customer;
using Subscription = Stripe.Subscription;

namespace Bit.Commercial.Core.Billing.Providers.Services;

public class ProviderBillingService(
    IBraintreeGateway braintreeGateway,
    IEventService eventService,
    IFeatureService featureService,
    IGlobalSettings globalSettings,
    ILogger<ProviderBillingService> logger,
    IOrganizationRepository organizationRepository,
    IPricingClient pricingClient,
    IProviderInvoiceItemRepository providerInvoiceItemRepository,
    IProviderOrganizationRepository providerOrganizationRepository,
    IProviderPlanRepository providerPlanRepository,
    IProviderUserRepository providerUserRepository,
    ISetupIntentCache setupIntentCache,
    IStripeAdapter stripeAdapter,
    ISubscriberService subscriberService,
    ITaxService taxService)
    : IProviderBillingService
{
    public async Task AddExistingOrganization(
        Provider provider,
        Organization organization,
        string key)
    {
        await stripeAdapter.SubscriptionUpdateAsync(organization.GatewaySubscriptionId,
            new SubscriptionUpdateOptions
            {
                CancelAtPeriodEnd = false
            });

        var subscription =
            await stripeAdapter.SubscriptionCancelAsync(organization.GatewaySubscriptionId,
                new SubscriptionCancelOptions
                {
                    CancellationDetails = new SubscriptionCancellationDetailsOptions
                    {
                        Comment = $"Organization was added to Provider with ID {provider.Id}"
                    },
                    InvoiceNow = true,
                    Prorate = true,
                    Expand = ["latest_invoice", "test_clock"]
                });

        var now = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;

        var wasTrialing = subscription.TrialEnd.HasValue && subscription.TrialEnd.Value > now;

        if (!wasTrialing && subscription.LatestInvoice.Status == StripeConstants.InvoiceStatus.Draft)
        {
            await stripeAdapter.InvoiceFinalizeInvoiceAsync(subscription.LatestInvoiceId,
                new InvoiceFinalizeOptions { AutoAdvance = true });
        }

        var managedPlanType = await GetManagedPlanTypeAsync(provider, organization);

        var plan = await pricingClient.GetPlanOrThrow(managedPlanType);
        organization.Plan = plan.Name;
        organization.PlanType = plan.Type;
        organization.MaxCollections = plan.PasswordManager.MaxCollections;
        organization.MaxStorageGb = plan.PasswordManager.BaseStorageGb;
        organization.UsePolicies = plan.HasPolicies;
        organization.UseSso = plan.HasSso;
        organization.UseOrganizationDomains = plan.HasOrganizationDomains;
        organization.UseGroups = plan.HasGroups;
        organization.UseEvents = plan.HasEvents;
        organization.UseDirectory = plan.HasDirectory;
        organization.UseTotp = plan.HasTotp;
        organization.Use2fa = plan.Has2fa;
        organization.UseApi = plan.HasApi;
        organization.UseResetPassword = plan.HasResetPassword;
        organization.SelfHost = plan.HasSelfHost;
        organization.UsersGetPremium = plan.UsersGetPremium;
        organization.UseCustomPermissions = plan.HasCustomPermissions;
        organization.UseScim = plan.HasScim;
        organization.UseKeyConnector = plan.HasKeyConnector;
        organization.MaxStorageGb = plan.PasswordManager.BaseStorageGb;
        organization.BillingEmail = provider.BillingEmail!;
        organization.GatewaySubscriptionId = null;
        organization.ExpirationDate = null;
        organization.MaxAutoscaleSeats = null;
        organization.Status = OrganizationStatusType.Managed;

        var providerOrganization = new ProviderOrganization
        {
            ProviderId = provider.Id,
            OrganizationId = organization.Id,
            Key = key
        };

        /*
         * We have to scale the provider's seats before the ProviderOrganization
         * row is inserted so the added organization's seats don't get double-counted.
         */
        await ScaleSeats(provider, organization.PlanType, organization.Seats!.Value);

        await Task.WhenAll(
            organizationRepository.ReplaceAsync(organization),
            providerOrganizationRepository.CreateAsync(providerOrganization)
        );

        var clientCustomer = await subscriberService.GetCustomer(organization);

        if (clientCustomer.Balance != 0)
        {
            await stripeAdapter.CustomerBalanceTransactionCreate(provider.GatewayCustomerId,
                new CustomerBalanceTransactionCreateOptions
                {
                    Amount = clientCustomer.Balance,
                    Currency = "USD",
                    Description = $"Unused, prorated time for client organization with ID {organization.Id}."
                });
        }

        await eventService.LogProviderOrganizationEventAsync(
            providerOrganization,
            EventType.ProviderOrganization_Added);
    }

    public async Task ChangePlan(ChangeProviderPlanCommand command)
    {
        var (provider, providerPlanId, newPlanType) = command;

        var providerPlan = await providerPlanRepository.GetByIdAsync(providerPlanId);

        if (providerPlan == null)
        {
            throw new BadRequestException("Provider plan not found.");
        }

        if (providerPlan.PlanType == newPlanType)
        {
            return;
        }

        var subscription = await subscriberService.GetSubscriptionOrThrow(provider);

        var oldPriceId = ProviderPriceAdapter.GetPriceId(provider, subscription, providerPlan.PlanType);
        var newPriceId = ProviderPriceAdapter.GetPriceId(provider, subscription, newPlanType);

        providerPlan.PlanType = newPlanType;
        await providerPlanRepository.ReplaceAsync(providerPlan);

        var oldSubscriptionItem = subscription.Items.SingleOrDefault(x => x.Price.Id == oldPriceId);

        var updateOptions = new SubscriptionUpdateOptions
        {
            Items =
            [
                new SubscriptionItemOptions
                {
                    Price = newPriceId,
                    Quantity = oldSubscriptionItem!.Quantity
                },
                new SubscriptionItemOptions
                {
                    Id = oldSubscriptionItem.Id,
                    Deleted = true
                }
            ]
        };

        await stripeAdapter.SubscriptionUpdateAsync(provider.GatewaySubscriptionId, updateOptions);

        // Refactor later to ?ChangeClientPlanCommand? (ProviderPlanId, ProviderId, OrganizationId)
        // 1. Retrieve PlanType and PlanName for ProviderPlan
        // 2. Assign PlanType & PlanName to Organization
        var providerOrganizations = await providerOrganizationRepository.GetManyDetailsByProviderAsync(providerPlan.ProviderId);

        var newPlan = await pricingClient.GetPlanOrThrow(newPlanType);

        foreach (var providerOrganization in providerOrganizations)
        {
            var organization = await organizationRepository.GetByIdAsync(providerOrganization.OrganizationId);
            if (organization == null)
            {
                throw new ConflictException($"Organization '{providerOrganization.Id}' not found.");
            }
            organization.PlanType = newPlanType;
            organization.Plan = newPlan.Name;
            await organizationRepository.ReplaceAsync(organization);
        }
    }

    public async Task CreateCustomerForClientOrganization(
        Provider provider,
        Organization organization)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(organization);

        if (!string.IsNullOrEmpty(organization.GatewayCustomerId))
        {
            logger.LogWarning("Client organization ({ID}) already has a populated {FieldName}", organization.Id, nameof(organization.GatewayCustomerId));

            return;
        }

        var providerCustomer = await subscriberService.GetCustomerOrThrow(provider, new CustomerGetOptions
        {
            Expand = ["tax", "tax_ids"]
        });

        var providerTaxId = providerCustomer.TaxIds.FirstOrDefault();

        var organizationDisplayName = organization.DisplayName();

        var customerCreateOptions = new CustomerCreateOptions
        {
            Address = new AddressOptions
            {
                Country = providerCustomer.Address?.Country,
                PostalCode = providerCustomer.Address?.PostalCode,
                Line1 = providerCustomer.Address?.Line1,
                Line2 = providerCustomer.Address?.Line2,
                City = providerCustomer.Address?.City,
                State = providerCustomer.Address?.State
            },
            Name = organizationDisplayName,
            Description = $"{provider.Name} Client Organization",
            Email = provider.BillingEmail,
            InvoiceSettings = new CustomerInvoiceSettingsOptions
            {
                CustomFields =
                [
                    new CustomerInvoiceSettingsCustomFieldOptions
                    {
                        Name = organization.SubscriberType(),
                        Value = organizationDisplayName.Length <= 30
                            ? organizationDisplayName
                            : organizationDisplayName[..30]
                    }
                ]
            },
            Metadata = new Dictionary<string, string>
            {
                { "region", globalSettings.BaseServiceUri.CloudRegion }
            },
            TaxIdData = providerTaxId == null ? null :
            [
                new CustomerTaxIdDataOptions
                {
                    Type = providerTaxId.Type,
                    Value = providerTaxId.Value
                }
            ]
        };

        var setNonUSBusinessUseToReverseCharge = featureService.IsEnabled(FeatureFlagKeys.PM21092_SetNonUSBusinessUseToReverseCharge);

        if (setNonUSBusinessUseToReverseCharge && providerCustomer.Address is not { Country: "US" })
        {
            customerCreateOptions.TaxExempt = StripeConstants.TaxExempt.Reverse;
        }

        var customer = await stripeAdapter.CustomerCreateAsync(customerCreateOptions);

        organization.GatewayCustomerId = customer.Id;

        await organizationRepository.ReplaceAsync(organization);
    }

    public async Task<byte[]> GenerateClientInvoiceReport(
        string invoiceId)
    {
        ArgumentException.ThrowIfNullOrEmpty(invoiceId);

        var invoiceItems = await providerInvoiceItemRepository.GetByInvoiceId(invoiceId);

        if (invoiceItems.Count == 0)
        {
            logger.LogError("No provider invoice item records were found for invoice ({InvoiceID})", invoiceId);

            return null;
        }

        var csvRows = invoiceItems.Select(ProviderClientInvoiceReportRow.From);

        using var memoryStream = new MemoryStream();

        await using var streamWriter = new StreamWriter(memoryStream);

        await using var csvWriter = new CsvWriter(streamWriter, CultureInfo.CurrentCulture);

        await csvWriter.WriteRecordsAsync(csvRows);

        await streamWriter.FlushAsync();

        memoryStream.Seek(0, SeekOrigin.Begin);

        return memoryStream.ToArray();
    }

    public async Task<IEnumerable<AddableOrganization>> GetAddableOrganizations(
        Provider provider,
        Guid userId)
    {
        var providerUser = await providerUserRepository.GetByProviderUserAsync(provider.Id, userId);

        if (providerUser is not { Status: ProviderUserStatusType.Confirmed })
        {
            throw new UnauthorizedAccessException();
        }

        var candidates = await organizationRepository.GetAddableToProviderByUserIdAsync(userId, provider.Type);

        var active = (await Task.WhenAll(candidates.Select(async organization =>
            {
                var subscription = await subscriberService.GetSubscription(organization);
                return (organization, subscription);
            })))
            .Where(pair => pair.subscription is
            {
                Status:
                    StripeConstants.SubscriptionStatus.Active or
                    StripeConstants.SubscriptionStatus.Trialing or
                    StripeConstants.SubscriptionStatus.PastDue
            }).ToList();

        if (active.Count == 0)
        {
            return [];
        }

        return await Task.WhenAll(active.Select(async pair =>
        {
            var (organization, _) = pair;

            var planName = await DerivePlanName(provider, organization);

            var addable = new AddableOrganization(
                organization.Id,
                organization.Name,
                planName,
                organization.Seats!.Value);

            if (providerUser.Type != ProviderUserType.ServiceUser)
            {
                return addable;
            }

            var applicablePlanType = await GetManagedPlanTypeAsync(provider, organization);

            var requiresPurchase =
                await SeatAdjustmentResultsInPurchase(provider, applicablePlanType, organization.Seats!.Value);

            return addable with { Disabled = requiresPurchase };
        }));

        async Task<string> DerivePlanName(Provider localProvider, Organization localOrganization)
        {
            if (localProvider.Type == ProviderType.Msp)
            {
                return localOrganization.PlanType switch
                {
                    var planType when PlanConstants.EnterprisePlanTypes.Contains(planType) => "Enterprise",
                    var planType when PlanConstants.TeamsPlanTypes.Contains(planType) => "Teams",
                    _ => throw new BillingException()
                };
            }

            var plan = await pricingClient.GetPlanOrThrow(localOrganization.PlanType);
            return plan.Name;
        }
    }

    public async Task ScaleSeats(
        Provider provider,
        PlanType planType,
        int seatAdjustment)
    {
        var providerPlan = await GetProviderPlanAsync(provider, planType);

        var seatMinimum = providerPlan.SeatMinimum ?? 0;

        var currentlyAssignedSeatTotal = await GetAssignedSeatTotalAsync(provider, planType);

        var newlyAssignedSeatTotal = currentlyAssignedSeatTotal + seatAdjustment;

        var scaleQuantityTo = CurrySeatScalingUpdate(
            provider,
            providerPlan,
            newlyAssignedSeatTotal);

        /*
         * Below the limit => Below the limit:
         * No subscription update required. We can safely update the provider's allocated seats.
         */
        if (currentlyAssignedSeatTotal <= seatMinimum &&
            newlyAssignedSeatTotal <= seatMinimum)
        {
            providerPlan.AllocatedSeats = newlyAssignedSeatTotal;

            await providerPlanRepository.ReplaceAsync(providerPlan);
        }
        /*
         * Below the limit => Above the limit:
         * We have to scale the subscription up from the seat minimum to the newly assigned seat total.
         */
        else if (currentlyAssignedSeatTotal <= seatMinimum &&
                 newlyAssignedSeatTotal > seatMinimum)
        {
            await scaleQuantityTo(newlyAssignedSeatTotal);
        }
        /*
         * Above the limit => Above the limit:
         * We have to scale the subscription from the currently assigned seat total to the newly assigned seat total.
         */
        else if (currentlyAssignedSeatTotal > seatMinimum &&
                 newlyAssignedSeatTotal > seatMinimum)
        {
            await scaleQuantityTo(newlyAssignedSeatTotal);
        }
        /*
         * Above the limit => Below the limit:
         * We have to scale the subscription down from the currently assigned seat total to the seat minimum.
         */
        else if (currentlyAssignedSeatTotal > seatMinimum &&
                 newlyAssignedSeatTotal <= seatMinimum)
        {
            await scaleQuantityTo(seatMinimum);
        }
    }

    public async Task<bool> SeatAdjustmentResultsInPurchase(
        Provider provider,
        PlanType planType,
        int seatAdjustment)
    {
        var providerPlan = await GetProviderPlanAsync(provider, planType);

        var seatMinimum = providerPlan.SeatMinimum;

        var currentlyAssignedSeatTotal = await GetAssignedSeatTotalAsync(provider, planType);

        var newlyAssignedSeatTotal = currentlyAssignedSeatTotal + seatAdjustment;

        return
            // Below the limit to above the limit
            (currentlyAssignedSeatTotal <= seatMinimum && newlyAssignedSeatTotal > seatMinimum) ||
            // Above the limit to further above the limit
            (currentlyAssignedSeatTotal > seatMinimum && newlyAssignedSeatTotal > seatMinimum && newlyAssignedSeatTotal > currentlyAssignedSeatTotal);
    }

    public async Task<Customer> SetupCustomer(
        Provider provider,
        TaxInfo taxInfo,
        TokenizedPaymentSource tokenizedPaymentSource = null)
    {
        if (taxInfo is not
            {
                BillingAddressCountry: not null and not "",
                BillingAddressPostalCode: not null and not ""
            })
        {
            logger.LogError("Cannot create customer for provider ({ProviderID}) without both a country and postal code", provider.Id);
            throw new BillingException();
        }

        var options = new CustomerCreateOptions
        {
            Address = new AddressOptions
            {
                Country = taxInfo.BillingAddressCountry,
                PostalCode = taxInfo.BillingAddressPostalCode,
                Line1 = taxInfo.BillingAddressLine1,
                Line2 = taxInfo.BillingAddressLine2,
                City = taxInfo.BillingAddressCity,
                State = taxInfo.BillingAddressState
            },
            Description = provider.DisplayBusinessName(),
            Email = provider.BillingEmail,
            InvoiceSettings = new CustomerInvoiceSettingsOptions
            {
                CustomFields =
                [
                    new CustomerInvoiceSettingsCustomFieldOptions
                    {
                        Name = provider.SubscriberType(),
                        Value = provider.DisplayName()?.Length <= 30
                            ? provider.DisplayName()
                            : provider.DisplayName()?[..30]
                    }
                ]
            },
            Metadata = new Dictionary<string, string>
            {
                { "region", globalSettings.BaseServiceUri.CloudRegion }
            }
        };

        var setNonUSBusinessUseToReverseCharge = featureService.IsEnabled(FeatureFlagKeys.PM21092_SetNonUSBusinessUseToReverseCharge);

        if (setNonUSBusinessUseToReverseCharge && taxInfo.BillingAddressCountry != "US")
        {
            options.TaxExempt = StripeConstants.TaxExempt.Reverse;
        }

        if (!string.IsNullOrEmpty(taxInfo.TaxIdNumber))
        {
            var taxIdType = taxService.GetStripeTaxCode(
                taxInfo.BillingAddressCountry,
                taxInfo.TaxIdNumber);

            options.TaxIdData =
            [
                new CustomerTaxIdDataOptions { Type = taxIdType, Value = taxInfo.TaxIdNumber }
            ];

            if (taxIdType == StripeConstants.TaxIdType.SpanishNIF)
            {
                options.TaxIdData.Add(new CustomerTaxIdDataOptions
                {
                    Type = StripeConstants.TaxIdType.EUVAT,
                    Value = $"ES{taxInfo.TaxIdNumber}"
                });
            }
        }

        var requireProviderPaymentMethodDuringSetup =
            featureService.IsEnabled(FeatureFlagKeys.PM19956_RequireProviderPaymentMethodDuringSetup);

        var braintreeCustomerId = "";

        if (requireProviderPaymentMethodDuringSetup)
        {
            if (tokenizedPaymentSource is not
                {
                    Type: PaymentMethodType.BankAccount or PaymentMethodType.Card or PaymentMethodType.PayPal,
                    Token: not null and not ""
                })
            {
                logger.LogError("Cannot create customer for provider ({ProviderID}) without a payment method", provider.Id);
                throw new BillingException();
            }

            var (type, token) = tokenizedPaymentSource;

            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (type)
            {
                case PaymentMethodType.BankAccount:
                    {
                        var setupIntent =
                            (await stripeAdapter.SetupIntentList(new SetupIntentListOptions { PaymentMethod = token }))
                            .FirstOrDefault();

                        if (setupIntent == null)
                        {
                            logger.LogError("Cannot create customer for provider ({ProviderID}) without a setup intent for their bank account", provider.Id);
                            throw new BillingException();
                        }

                        await setupIntentCache.Set(provider.Id, setupIntent.Id);
                        break;
                    }
                case PaymentMethodType.Card:
                    {
                        options.PaymentMethod = token;
                        options.InvoiceSettings.DefaultPaymentMethod = token;
                        break;
                    }
                case PaymentMethodType.PayPal:
                    {
                        braintreeCustomerId = await subscriberService.CreateBraintreeCustomer(provider, token);
                        options.Metadata[BraintreeCustomerIdKey] = braintreeCustomerId;
                        break;
                    }
            }
        }

        try
        {
            return await stripeAdapter.CustomerCreateAsync(options);
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
            if (requireProviderPaymentMethodDuringSetup && tokenizedPaymentSource != null)
            {
                // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
                switch (tokenizedPaymentSource.Type)
                {
                    case PaymentMethodType.BankAccount:
                        {
                            var setupIntentId = await setupIntentCache.Get(provider.Id);
                            await stripeAdapter.SetupIntentCancel(setupIntentId,
                                new SetupIntentCancelOptions { CancellationReason = "abandoned" });
                            await setupIntentCache.Remove(provider.Id);
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

    public async Task<Subscription> SetupSubscription(
        Provider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var customerGetOptions = new CustomerGetOptions { Expand = ["tax", "tax_ids"] };
        var customer = await subscriberService.GetCustomerOrThrow(provider, customerGetOptions);

        var providerPlans = await providerPlanRepository.GetByProviderId(provider.Id);

        if (providerPlans.Count == 0)
        {
            logger.LogError("Cannot start subscription for provider ({ProviderID}) that has no configured plans", provider.Id);

            throw new BillingException();
        }

        var subscriptionItemOptionsList = new List<SubscriptionItemOptions>();

        foreach (var providerPlan in providerPlans)
        {
            var plan = await pricingClient.GetPlanOrThrow(providerPlan.PlanType);

            if (!providerPlan.IsConfigured())
            {
                logger.LogError("Cannot start subscription for provider ({ProviderID}) that has no configured {ProviderName} plan", provider.Id, plan.Name);
                throw new BillingException();
            }

            var priceId = ProviderPriceAdapter.GetActivePriceId(provider, providerPlan.PlanType);

            subscriptionItemOptionsList.Add(new SubscriptionItemOptions
            {
                Price = priceId,
                Quantity = providerPlan.SeatMinimum
            });
        }

        var requireProviderPaymentMethodDuringSetup =
            featureService.IsEnabled(FeatureFlagKeys.PM19956_RequireProviderPaymentMethodDuringSetup);

        var setupIntentId = await setupIntentCache.Get(provider.Id);

        var setupIntent = !string.IsNullOrEmpty(setupIntentId)
            ? await stripeAdapter.SetupIntentGet(setupIntentId, new SetupIntentGetOptions
            {
                Expand = ["payment_method"]
            })
            : null;

        var usePaymentMethod =
            requireProviderPaymentMethodDuringSetup &&
            setupIntent != null &&
            (!string.IsNullOrEmpty(customer.InvoiceSettings.DefaultPaymentMethodId) ||
             customer.Metadata.ContainsKey(BraintreeCustomerIdKey) ||
             setupIntent.IsUnverifiedBankAccount());

        int? trialPeriodDays = provider.Type switch
        {
            ProviderType.Msp when usePaymentMethod => 14,
            ProviderType.BusinessUnit when usePaymentMethod => 4,
            _ => null
        };

        var subscriptionCreateOptions = new SubscriptionCreateOptions
        {
            CollectionMethod = usePaymentMethod ?
                StripeConstants.CollectionMethod.ChargeAutomatically : StripeConstants.CollectionMethod.SendInvoice,
            Customer = customer.Id,
            DaysUntilDue = usePaymentMethod ? null : 30,
            Discounts = !string.IsNullOrEmpty(provider.DiscountId) ? [new SubscriptionDiscountOptions { Coupon = provider.DiscountId }] : null,
            Items = subscriptionItemOptionsList,
            Metadata = new Dictionary<string, string>
            {
                { "providerId", provider.Id.ToString() }
            },
            OffSession = true,
            ProrationBehavior = StripeConstants.ProrationBehavior.CreateProrations,
            TrialPeriodDays = trialPeriodDays
        };

        var setNonUSBusinessUseToReverseCharge =
            featureService.IsEnabled(FeatureFlagKeys.PM21092_SetNonUSBusinessUseToReverseCharge);

        if (setNonUSBusinessUseToReverseCharge)
        {
            subscriptionCreateOptions.AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true };
        }
        else if (customer.HasRecognizedTaxLocation())
        {
            subscriptionCreateOptions.AutomaticTax = new SubscriptionAutomaticTaxOptions
            {
                Enabled = customer.Address.Country == "US" ||
                          customer.TaxIds.Any()
            };
        }

        try
        {
            var subscription = await stripeAdapter.SubscriptionCreateAsync(subscriptionCreateOptions);

            if (subscription is
                {
                    Status: StripeConstants.SubscriptionStatus.Active or StripeConstants.SubscriptionStatus.Trialing
                })
            {
                return subscription;
            }

            logger.LogError(
                "Newly created provider ({ProviderID}) subscription ({SubscriptionID}) has inactive status: {Status}",
                provider.Id,
                subscription.Id,
                subscription.Status);

            throw new BillingException();
        }
        catch (StripeException stripeException) when (stripeException.StripeError?.Code == StripeConstants.ErrorCodes.CustomerTaxLocationInvalid)
        {
            throw new BadRequestException("Your location wasn't recognized. Please ensure your country and postal code are valid.");
        }
    }

    public async Task UpdatePaymentMethod(
        Provider provider,
        TokenizedPaymentSource tokenizedPaymentSource,
        TaxInformation taxInformation)
    {
        await Task.WhenAll(
            subscriberService.UpdatePaymentSource(provider, tokenizedPaymentSource),
            subscriberService.UpdateTaxInformation(provider, taxInformation));

        await stripeAdapter.SubscriptionUpdateAsync(provider.GatewaySubscriptionId,
            new SubscriptionUpdateOptions { CollectionMethod = StripeConstants.CollectionMethod.ChargeAutomatically });
    }

    public async Task UpdateSeatMinimums(UpdateProviderSeatMinimumsCommand command)
    {
        var (provider, updatedPlanConfigurations) = command;

        if (updatedPlanConfigurations.Any(x => x.SeatsMinimum < 0))
        {
            throw new BadRequestException("Provider seat minimums must be at least 0.");
        }

        var subscription = await subscriberService.GetSubscriptionOrThrow(provider);

        var subscriptionItemOptionsList = new List<SubscriptionItemOptions>();

        var providerPlans = await providerPlanRepository.GetByProviderId(provider.Id);

        foreach (var updatedPlanConfiguration in updatedPlanConfigurations)
        {
            var (updatedPlanType, updatedSeatMinimum) = updatedPlanConfiguration;

            var providerPlan =
                providerPlans.Single(providerPlan => providerPlan.PlanType == updatedPlanType);

            if (providerPlan.SeatMinimum != updatedSeatMinimum)
            {
                var priceId = ProviderPriceAdapter.GetPriceId(provider, subscription, updatedPlanType);

                var subscriptionItem = subscription.Items.First(item => item.Price.Id == priceId);

                if (providerPlan.PurchasedSeats == 0)
                {
                    if (providerPlan.AllocatedSeats > updatedSeatMinimum)
                    {
                        providerPlan.PurchasedSeats = providerPlan.AllocatedSeats - updatedSeatMinimum;

                        subscriptionItemOptionsList.Add(new SubscriptionItemOptions
                        {
                            Id = subscriptionItem.Id,
                            Price = priceId,
                            Quantity = providerPlan.AllocatedSeats
                        });
                    }
                    else
                    {
                        subscriptionItemOptionsList.Add(new SubscriptionItemOptions
                        {
                            Id = subscriptionItem.Id,
                            Price = priceId,
                            Quantity = updatedSeatMinimum
                        });
                    }
                }
                else
                {
                    var totalSeats = providerPlan.SeatMinimum + providerPlan.PurchasedSeats;

                    if (updatedSeatMinimum <= totalSeats)
                    {
                        providerPlan.PurchasedSeats = totalSeats - updatedSeatMinimum;
                    }
                    else
                    {
                        providerPlan.PurchasedSeats = 0;
                        subscriptionItemOptionsList.Add(new SubscriptionItemOptions
                        {
                            Id = subscriptionItem.Id,
                            Price = priceId,
                            Quantity = updatedSeatMinimum
                        });
                    }
                }

                providerPlan.SeatMinimum = updatedSeatMinimum;

                await providerPlanRepository.ReplaceAsync(providerPlan);
            }
        }

        if (subscriptionItemOptionsList.Count > 0)
        {
            await stripeAdapter.SubscriptionUpdateAsync(provider.GatewaySubscriptionId,
                new SubscriptionUpdateOptions { Items = subscriptionItemOptionsList });
        }
    }

    private Func<int, Task> CurrySeatScalingUpdate(
        Provider provider,
        ProviderPlan providerPlan,
        int newlyAssignedSeats) => async newlySubscribedSeats =>
    {
        var subscription = await subscriberService.GetSubscriptionOrThrow(provider);

        var priceId = ProviderPriceAdapter.GetPriceId(provider, subscription, providerPlan.PlanType);

        var item = subscription.Items.First(item => item.Price.Id == priceId);

        await stripeAdapter.SubscriptionUpdateAsync(provider.GatewaySubscriptionId, new SubscriptionUpdateOptions
        {
            Items = [
                new SubscriptionItemOptions
                {
                    Id = item.Id,
                    Price = priceId,
                    Quantity = newlySubscribedSeats
                }
            ]
        });

        var newlyPurchasedSeats = newlySubscribedSeats > providerPlan.SeatMinimum
            ? newlySubscribedSeats - providerPlan.SeatMinimum
            : 0;

        providerPlan.PurchasedSeats = newlyPurchasedSeats;
        providerPlan.AllocatedSeats = newlyAssignedSeats;

        await providerPlanRepository.ReplaceAsync(providerPlan);
    };

    // TODO: Replace with SPROC
    private async Task<int> GetAssignedSeatTotalAsync(Provider provider, PlanType planType)
    {
        var providerOrganizations =
            await providerOrganizationRepository.GetManyDetailsByProviderAsync(provider.Id);

        var plan = await pricingClient.GetPlanOrThrow(planType);

        return providerOrganizations
            .Where(providerOrganization => providerOrganization.Plan == plan.Name && providerOrganization.Status == OrganizationStatusType.Managed)
            .Sum(providerOrganization => providerOrganization.Seats ?? 0);
    }

    // TODO: Replace with SPROC
    private async Task<ProviderPlan> GetProviderPlanAsync(Provider provider, PlanType planType)
    {
        var providerPlans = await providerPlanRepository.GetByProviderId(provider.Id);

        var providerPlan = providerPlans.FirstOrDefault(x => x.PlanType == planType);

        if (providerPlan == null || !providerPlan.IsConfigured())
        {
            throw new BillingException(message: "Provider plan is missing or misconfigured");
        }

        return providerPlan;
    }

    private async Task<PlanType> GetManagedPlanTypeAsync(
        Provider provider,
        Organization organization)
    {
        if (provider.Type == ProviderType.BusinessUnit)
        {
            return (await providerPlanRepository.GetByProviderId(provider.Id)).First().PlanType;
        }

        return organization.PlanType switch
        {
            var planType when PlanConstants.TeamsPlanTypes.Contains(planType) => PlanType.TeamsMonthly,
            var planType when PlanConstants.EnterprisePlanTypes.Contains(planType) => PlanType.EnterpriseMonthly,
            _ => throw new BillingException()
        };
    }
}
