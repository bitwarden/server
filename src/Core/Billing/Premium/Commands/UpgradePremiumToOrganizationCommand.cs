using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;
using OneOf.Types;
using Stripe;

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
    /// <param name="targetPlanType">The target organization plan type to upgrade to.</param>
    /// <param name="billingAddress">The billing address for tax calculation.</param>
    /// <returns>A billing command result indicating success or failure with appropriate error details.</returns>
    Task<BillingCommandResult<None>> Run(
        User user,
        string organizationName,
        string key,
        PlanType targetPlanType,
        Payment.Models.BillingAddress billingAddress);
}

public class UpgradePremiumToOrganizationCommand(
    ILogger<UpgradePremiumToOrganizationCommand> logger,
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter,
    IUserService userService,
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationApiKeyRepository organizationApiKeyRepository,
    IApplicationCacheService applicationCacheService)
    : BaseBillingCommand<UpgradePremiumToOrganizationCommand>(logger), IUpgradePremiumToOrganizationCommand
{
    public Task<BillingCommandResult<None>> Run(
        User user,
        string organizationName,
        string key,
        PlanType targetPlanType,
        Payment.Models.BillingAddress billingAddress) => HandleAsync<None>(async () =>
    {
        // Validate that the user has an active Premium subscription
        if (user is not { Premium: true, GatewaySubscriptionId: not null and not "" })
        {
            return new BadRequest("User does not have an active Premium subscription.");
        }

        // Hardcode seats to 1 for upgrade flow
        const int seats = 1;

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

        // Build the list of subscription item updates
        var subscriptionItemOptions = new List<SubscriptionItemOptions>();

        // Delete the storage item if it exists for this user's plan
        var storageItem = currentSubscription.Items.Data.FirstOrDefault(i =>
            i.Price.Id == usersPremiumPlan.Storage.StripePriceId);

        if (storageItem != null)
        {
            subscriptionItemOptions.Add(new SubscriptionItemOptions
            {
                Id = storageItem.Id,
                Deleted = true
            });
        }

        // Add new organization subscription items
        if (targetPlan.HasNonSeatBasedPasswordManagerPlan())
        {
            subscriptionItemOptions.Add(new SubscriptionItemOptions
            {
                Id = passwordManagerItem.Id,
                Price = targetPlan.PasswordManager.StripePlanId,
                Quantity = 1
            });
        }
        else
        {
            subscriptionItemOptions.Add(new SubscriptionItemOptions
            {
                Id = passwordManagerItem.Id,
                Price = targetPlan.PasswordManager.StripeSeatPlanId,
                Quantity = seats
            });
        }

        // Generate organization ID early to include in metadata
        var organizationId = CoreHelpers.GenerateComb();

        // Build the subscription update options
        var subscriptionUpdateOptions = new SubscriptionUpdateOptions
        {
            Items = subscriptionItemOptions,
            ProrationBehavior = StripeConstants.ProrationBehavior.AlwaysInvoice,
            BillingCycleAnchor = SubscriptionBillingCycleAnchor.Unchanged,
            AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true },
            Metadata = new Dictionary<string, string>
            {
                [StripeConstants.MetadataKeys.OrganizationId] = organizationId.ToString(),
                [StripeConstants.MetadataKeys.UserId] = string.Empty // Remove userId to unlink subscription from User
            }
        };

        // Create the Organization entity
        var organization = new Organization
        {
            Id = organizationId,
            Name = organizationName,
            BillingEmail = user.Email,
            PlanType = targetPlan.Type,
            Seats = seats,
            MaxCollections = targetPlan.PasswordManager.MaxCollections,
            MaxStorageGb = targetPlan.PasswordManager.BaseStorageGb,
            UsePolicies = targetPlan.HasPolicies,
            UseMyItems = targetPlan.HasPolicies, // TODO: use the plan property when added (PM-32366)
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
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
            Status = OrganizationStatusType.Created,
            UsePasswordManager = true,
            UseSecretsManager = false,
            UseOrganizationDomains = targetPlan.HasOrganizationDomains,
            GatewayCustomerId = user.GatewayCustomerId,
            GatewaySubscriptionId = currentSubscription.Id
        };

        // Update customer billing address for tax calculation
        await stripeAdapter.UpdateCustomerAsync(user.GatewayCustomerId, new CustomerUpdateOptions
        {
            Address = new AddressOptions
            {
                Country = billingAddress.Country,
                PostalCode = billingAddress.PostalCode
            }
        });

        // Update the subscription in Stripe
        await stripeAdapter.UpdateSubscriptionAsync(currentSubscription.Id, subscriptionUpdateOptions);

        // Save the organization
        await organizationRepository.CreateAsync(organization);

        // Create organization API key
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

        // Remove subscription from user
        user.Premium = false;
        user.PremiumExpirationDate = null;
        user.GatewaySubscriptionId = null;
        user.GatewayCustomerId = null;
        user.RevisionDate = DateTime.UtcNow;
        await userService.SaveUserAsync(user);

        return new None();
    });
}
