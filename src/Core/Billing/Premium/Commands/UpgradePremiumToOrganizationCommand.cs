using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions.Models;
using Bit.Core.Billing.Tax.Utilities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;
using Stripe;
using static Bit.Core.Billing.Constants.StripeConstants;
using static Bit.Core.Billing.Utilities;
using PremiumPlan = Bit.Core.Billing.Pricing.Premium.Plan;

namespace Bit.Core.Billing.Premium.Commands;

/// <summary>
/// Upgrades a user's Premium subscription to an Organization plan by creating a new Organization
/// and transferring the subscription from the User to the Organization.
/// </summary>
public interface IUpgradePremiumToOrganizationCommand
{
    /// <summary>
    /// Upgrades a Premium subscription to an Organization subscription.
    /// </summary>
    /// <param name="user">The user with an active Premium subscription to upgrade.</param>
    /// <param name="organizationName">The name for the new organization.</param>
    /// <param name="key">The encrypted organization key for the owner.</param>
    /// <param name="publicKey">The organization's public key.</param>
    /// <param name="encryptedPrivateKey">The organization's encrypted private key.</param>
    /// <param name="collectionName">Optional name for the default collection.</param>
    /// <param name="targetPlanType">The target organization plan type to upgrade to.</param>
    /// <param name="billingAddress">The billing address for tax calculation.</param>
    /// <returns>A billing command result containing the new organization ID on success, or error details on failure.</returns>
    Task<BillingCommandResult<Guid>> Run(
        User user,
        string organizationName,
        string key,
        string publicKey,
        string encryptedPrivateKey,
        string? collectionName,
        PlanType targetPlanType,
        BillingAddress billingAddress);
}

public class UpgradePremiumToOrganizationCommand(
    ILogger<UpgradePremiumToOrganizationCommand> logger,
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter,
    IPriceIncreaseScheduler priceIncreaseScheduler,
    IUserService userService,
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationApiKeyRepository organizationApiKeyRepository,
    ICollectionRepository collectionRepository,
    IBraintreeService braintreeService,
    IGetPaymentMethodQuery getPaymentMethodQuery,
    IApplicationCacheService applicationCacheService,
    IPushNotificationService pushNotificationService)
    : BaseBillingCommand<UpgradePremiumToOrganizationCommand>(logger), IUpgradePremiumToOrganizationCommand
{
    private readonly ILogger<UpgradePremiumToOrganizationCommand> _logger = logger;

    public Task<BillingCommandResult<Guid>> Run(
        User user,
        string organizationName,
        string key,
        string publicKey,
        string encryptedPrivateKey,
        string? collectionName,
        PlanType targetPlanType,
        BillingAddress billingAddress) => HandleAsync<Guid>(async () =>
    {
        // Validate that the user has an active Premium subscription
        if (user is not { Premium: true, GatewayCustomerId: not null and not "", GatewaySubscriptionId: not null and not "" })
        {
            return new BadRequest("User does not have an active Premium subscription.");
        }

        var paymentMethod = await getPaymentMethodQuery.Run(user);
        if (paymentMethod is null)
        {
            return new BadRequest("No payment method found for the user. Please add a payment method to upgrade to Organization plan.");
        }

        if (paymentMethod.IsBankAccount && paymentMethod.AsT0.HostedVerificationUrl is not null)
        {
            return new BadRequest("Unverified bank accounts are not supported for upgrading to an Organization plan. Please use a card or PayPal.");
        }

        // Fetch the current Premium subscription from Stripe
        var currentSubscription = await stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId);

        // Fetch all premium plans to find which specific plan the user is on
        var premiumPlans = await pricingClient.ListPremiumPlans();

        // Find the password manager subscription item (seat, not storage) and match it to a plan
        var passwordManagerItem = currentSubscription.Items.Data.FirstOrDefault(i =>
            premiumPlans.Any(p => p.Seat.StripePriceId == i.Price.Id));

        if (passwordManagerItem == null)
        {
            return new BadRequest("Premium subscription password manager item not found.");
        }

        var usersPremiumPlan = premiumPlans.First(p => p.Seat.StripePriceId == passwordManagerItem.Price.Id);

        // Get the target organization plan
        var targetPlan = await pricingClient.GetPlanOrThrow(targetPlanType);

        var subscriptionItemOptions = BuildSubscriptionItemOptions(
            currentSubscription, usersPremiumPlan, targetPlan, passwordManagerItem);

        // Generate organization ID early to include in metadata
        var organizationId = CoreHelpers.GenerateComb();

        // Create the Organization entity
        var organization = BuildOrganization(
            organizationId, user, organizationName, publicKey, encryptedPrivateKey, targetPlan, currentSubscription.Id);

        // Update customer billing address for tax calculation
        var customer = await stripeAdapter.UpdateCustomerAsync(user.GatewayCustomerId,
            new CustomerUpdateOptions
            {
                Address = new AddressOptions
                {
                    Country = billingAddress.Country,
                    PostalCode = billingAddress.PostalCode
                },
                TaxExempt = TaxHelpers.DetermineTaxExemptStatus(billingAddress.Country),
            });

        await UpdateSubscriptionAsync(currentSubscription.Id, organizationId, customer, subscriptionItemOptions);

        // Add tax ID to the customer for accurate tax calculation if provided
        if (billingAddress.TaxId != null)
        {
            await AddTaxIdToCustomerAsync(user.GatewayCustomerId!, billingAddress.TaxId);
        }

        var organizationUser = await SaveOrganizationAsync(organization, user, key);

        // Create a default collection if a collection name is provided
        if (!string.IsNullOrWhiteSpace(collectionName))
        {
            await CreateDefaultCollectionAsync(organization, organizationUser, collectionName);
        }

        // Remove subscription from a user
        user.Premium = false;
        user.PremiumExpirationDate = null;
        user.GatewaySubscriptionId = null;
        user.GatewayCustomerId = null;
        user.RevisionDate = DateTime.UtcNow;

        await userService.SaveUserAsync(user);
        await SendPremiumStatusNotificationAsync(user);

        return organization.Id;

    });

    private async Task SendPremiumStatusNotificationAsync(User user) =>
        await pushNotificationService.PushAsync(new PushNotification<PremiumStatusPushNotification>
        {
            Type = PushType.PremiumStatusChanged,
            Target = NotificationTarget.User,
            TargetId = user.Id,
            Payload = new PremiumStatusPushNotification
            {
                UserId = user.Id,
                Premium = user.Premium,
            },
            ExcludeCurrentContext = false,
        });

    private List<SubscriptionItemOptions> BuildSubscriptionItemOptions(
        Subscription currentSubscription,
        PremiumPlan usersPremiumPlan,
        Core.Models.StaticStore.Plan targetPlan,
        SubscriptionItem passwordManagerItem)
    {
        var isNonSeatBasedPmPlan = targetPlan.HasNonSeatBasedPasswordManagerPlan();

        // Build the list of subscription item updates
        var options = new List<SubscriptionItemOptions>();

        // Delete the storage item if it exists for this user's plan
        var storageItem = currentSubscription.Items.Data.FirstOrDefault(i =>
            i.Price.Id == usersPremiumPlan.Storage.StripePriceId);

        if (storageItem != null)
        {
            options.Add(new SubscriptionItemOptions { Id = storageItem.Id, Deleted = true });
        }

        // Add new organization subscription items
        options.Add(new SubscriptionItemOptions
        {
            Id = passwordManagerItem.Id,
            Price = isNonSeatBasedPmPlan
                ? targetPlan.PasswordManager.StripePlanId
                : targetPlan.PasswordManager.StripeSeatPlanId,
            Quantity = 1
        });

        return options;
    }

    private Organization BuildOrganization(
        Guid organizationId,
        User user,
        string organizationName,
        string publicKey,
        string encryptedPrivateKey,
        Core.Models.StaticStore.Plan targetPlan,
        string subscriptionId)
    {
        var isNonSeatBasedPmPlan = targetPlan.HasNonSeatBasedPasswordManagerPlan();

        // if the target plan is non-seat-based, set seats to the base seats of the target plan, otherwise set to 1
        var initialSeats = isNonSeatBasedPmPlan ? targetPlan.PasswordManager.BaseSeats : 1;

        return new Organization
        {
            Id = organizationId,
            Name = organizationName,
            BillingEmail = user.Email,
            PlanType = targetPlan.Type,
            Seats = initialSeats,
            MaxCollections = targetPlan.PasswordManager.MaxCollections,
            MaxStorageGb = targetPlan.PasswordManager.BaseStorageGb,
            UsePolicies = targetPlan.HasPolicies,
            UseMyItems = targetPlan.HasMyItems,
            UseSso = targetPlan.HasSso,
            UseGroups = targetPlan.HasGroups,
            UseEvents = targetPlan.HasEvents,
            UseDirectory = targetPlan.HasDirectory,
            UseTotp = targetPlan.HasTotp,
            Use2fa = targetPlan.Has2fa,
            UseApi = targetPlan.HasApi,
            UseResetPassword = targetPlan.HasResetPassword,
            SelfHost = targetPlan.HasSelfHost,
            UsersGetPremium = targetPlan.UsersGetPremium,
            UseCustomPermissions = targetPlan.HasCustomPermissions,
            UseScim = targetPlan.HasScim,
            Plan = targetPlan.Name,
            Gateway = GatewayType.Stripe,
            Enabled = true,
            LicenseKey = CoreHelpers.SecureRandomString(20),
            PublicKey = publicKey,
            PrivateKey = encryptedPrivateKey,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
            Status = OrganizationStatusType.Created,
            UsePasswordManager = true,
            UseSecretsManager = false,
            UseOrganizationDomains = targetPlan.HasOrganizationDomains,
            GatewayCustomerId = user.GatewayCustomerId,
            GatewaySubscriptionId = subscriptionId
        };
    }

    private async Task UpdateSubscriptionAsync(
        string subscriptionId,
        Guid organizationId,
        Customer customer,
        List<SubscriptionItemOptions> subscriptionItemOptions)
    {
        var usingPayPal = customer.Metadata?.ContainsKey(BraintreeCustomerIdKey) ?? false;

        // Build the subscription update options
        var subscriptionUpdateOptions = new SubscriptionUpdateOptions
        {
            Items = subscriptionItemOptions,
            ProrationBehavior = ProrationBehavior.AlwaysInvoice,
            BillingCycleAnchor = SubscriptionBillingCycleAnchor.Unchanged,
            AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true },
            Metadata = new Dictionary<string, string>
            {
                [MetadataKeys.OrganizationId] = organizationId.ToString(),
                [MetadataKeys.UserId] = string.Empty // Remove userId to unlink the subscription from User
            },
            PaymentBehavior = usingPayPal ? PaymentBehavior.DefaultIncomplete : null
        };

        // Update the subscription in Stripe
        var subscription = await stripeAdapter.UpdateSubscriptionAsync(subscriptionId, subscriptionUpdateOptions);

        // If using PayPal, pay the invoice via Braintree
        if (usingPayPal)
        {
            await PayInvoiceUsingPayPalAsync(subscription, organizationId);
        }
    }

    private async Task<OrganizationUser> SaveOrganizationAsync(
        Organization organization,
        User user,
        string key)
    {
        await priceIncreaseScheduler.Release(user.GatewayCustomerId, currentSubscription.Id);

        // Update the subscription in Stripe
        await stripeAdapter.UpdateSubscriptionAsync(currentSubscription.Id, subscriptionUpdateOptions);
    }
        // Save the organization
        await organizationRepository.CreateAsync(organization);

        // Create the organization API key
        await organizationApiKeyRepository.CreateAsync(new OrganizationApiKey
        {
            OrganizationId = organization.Id,
            ApiKey = CoreHelpers.SecureRandomString(30),
            Type = OrganizationApiKeyType.Default,
            RevisionDate = DateTime.UtcNow,
        });

        // Update cache
        await applicationCacheService.UpsertOrganizationAbilityAsync(organization);

        // Create OrganizationUser for the upgrading user as owner
        var organizationUser = new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Key = key,
            AccessSecretsManager = false,
            Type = OrganizationUserType.Owner,
            Status = OrganizationUserStatusType.Confirmed,
            CreationDate = organization.CreationDate,
            RevisionDate = organization.CreationDate
        };
        organizationUser.SetNewId();
        await organizationUserRepository.CreateAsync(organizationUser);

        return organizationUser;
    }

    private async Task CreateDefaultCollectionAsync(
        Organization organization,
        OrganizationUser organizationUser,
        string collectionName)
    {
        try
        {
            // Give the owner Can Manage access over the default collection
            List<CollectionAccessSelection> defaultOwnerAccess =
                [new() { Id = organizationUser.Id, HidePasswords = false, ReadOnly = false, Manage = true }];

            var defaultCollection = new Collection
            {
                Name = collectionName,
                OrganizationId = organization.Id,
                CreationDate = organization.CreationDate,
                RevisionDate = organization.CreationDate
            };
            await collectionRepository.CreateAsync(defaultCollection, null, defaultOwnerAccess);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "{Command}: Failed to create default collection for organization {OrganizationId}. Organization upgrade will continue.",
                CommandName, organization.Id);
            // Continue - organization is fully functional without default collection
        }
    }

    private async Task PayInvoiceUsingPayPalAsync(Subscription subscription, Guid organizationId)
    {
        var invoice = await stripeAdapter.UpdateInvoiceAsync(subscription.LatestInvoiceId,
            new InvoiceUpdateOptions { AutoAdvance = false, Expand = ["customer"] });

        await braintreeService.PayInvoice(new OrganizationId(organizationId), invoice);
    }

    /// <summary>
    /// Adds a tax ID to the Stripe customer for accurate tax calculation.
    /// If the tax ID is a Spanish NIF, also adds the corresponding EU VAT ID.
    /// </summary>
    /// <param name="customerId">The Stripe customer ID to add the tax ID to.</param>
    /// <param name="taxId">The tax ID to add, including the type and value.</param>
    private async Task AddTaxIdToCustomerAsync(string customerId, TaxID taxId)
    {
        await stripeAdapter.CreateTaxIdAsync(customerId,
            new TaxIdCreateOptions { Type = taxId.Code, Value = taxId.Value });

        if (taxId.Code == TaxIdType.SpanishNIF)
        {
            await stripeAdapter.CreateTaxIdAsync(customerId,
                new TaxIdCreateOptions { Type = TaxIdType.EUVAT, Value = $"ES{taxId.Value}" });
        }
    }
}
