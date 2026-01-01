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
    /// <param name="targetPlanType">The target organization plan type to upgrade to.</param>
    /// <param name="seats">The number of seats for the organization plan.</param>
    /// <param name="premiumAccess">Whether to include premium access for the organization.</param>
    /// <param name="storage">Additional storage in GB for the organization.</param>
    /// <param name="trialEndDate">The trial end date to apply to the upgraded subscription.</param>
    /// <returns>A billing command result indicating success or failure with appropriate error details.</returns>
    Task<BillingCommandResult<None>> Run(
        User user,
        PlanType targetPlanType,
        int seats,
        bool premiumAccess,
        int? storage,
        DateTime? trialEndDate);
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
        PlanType targetPlanType,
        int seats,
        bool premiumAccess,
        int? storage,
        DateTime? trialEndDate) => HandleAsync<None>(async () =>
    {
        // Validate that the user has an active Premium subscription
        if (user is not { Premium: true, GatewaySubscriptionId: not null and not "" })
        {
            return new BadRequest("User does not have an active Premium subscription.");
        }

        if (seats < 1)
        {
            return new BadRequest("Seats must be at least 1.");
        }

        if (trialEndDate.HasValue && trialEndDate.Value < DateTime.UtcNow)
        {
            return new BadRequest("Trial end date cannot be in the past.");
        }

        // Fetch the current Premium subscription from Stripe
        var currentSubscription = await stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId);

        // Get the target organization plan
        var targetPlan = await pricingClient.GetPlanOrThrow(targetPlanType);

        // Validate plan supports requested features
        if (premiumAccess && string.IsNullOrEmpty(targetPlan.PasswordManager.StripePremiumAccessPlanId))
        {
            return new BadRequest("The selected plan does not support premium access.");
        }

        if (storage is > 0 && string.IsNullOrEmpty(targetPlan.PasswordManager.StripeStoragePlanId))
        {
            return new BadRequest("The selected plan does not support additional storage.");
        }

        // Build the list of subscription item updates
        var subscriptionItemOptions = new List<SubscriptionItemOptions>();

        // Mark existing Premium subscription items for deletion
        foreach (var item in currentSubscription.Items.Data)
        {
            subscriptionItemOptions.Add(new SubscriptionItemOptions
            {
                Id = item.Id,
                Deleted = true
            });
        }

        // Add new organization subscription items
        if (targetPlan.HasNonSeatBasedPasswordManagerPlan())
        {
            subscriptionItemOptions.Add(new SubscriptionItemOptions
            {
                Price = targetPlan.PasswordManager.StripePlanId,
                Quantity = 1
            });
        }
        else
        {
            subscriptionItemOptions.Add(new SubscriptionItemOptions
            {
                Price = targetPlan.PasswordManager.StripeSeatPlanId,
                Quantity = seats
            });
        }

        if (premiumAccess)
        {
            subscriptionItemOptions.Add(new SubscriptionItemOptions
            {
                Price = targetPlan.PasswordManager.StripePremiumAccessPlanId,
                Quantity = 1
            });
        }

        if (storage is > 0)
        {
            subscriptionItemOptions.Add(new SubscriptionItemOptions
            {
                Price = targetPlan.PasswordManager.StripeStoragePlanId,
                Quantity = storage
            });
        }

        // Build the subscription update options
        var subscriptionUpdateOptions = new SubscriptionUpdateOptions
        {
            Items = subscriptionItemOptions,
            ProrationBehavior = StripeConstants.ProrationBehavior.None,
            Metadata = new Dictionary<string, string>(currentSubscription.Metadata ?? new Dictionary<string, string>())
            {
                ["premium_upgrade_metadata"] = currentSubscription.Items.Data
                    .Select(item => item.Price.Id)
                    .FirstOrDefault() ?? "premium"
            }
        };

        // Apply trial period if specified
        if (trialEndDate.HasValue)
        {
            subscriptionUpdateOptions.TrialEnd = trialEndDate.Value;
        }

        // Create the Organization entity
        var organization = new Organization
        {
            Id = CoreHelpers.GenerateComb(),
            Name = $"{user.Email}'s Organization",
            BillingEmail = user.Email,
            PlanType = targetPlan.Type,
            Seats = (short)seats,
            MaxCollections = targetPlan.PasswordManager.MaxCollections,
            MaxStorageGb = (short)(targetPlan.PasswordManager.BaseStorageGb + (storage ?? 0)),
            UsePolicies = targetPlan.HasPolicies,
            UseSso = targetPlan.HasSso,
            UseGroups = targetPlan.HasGroups,
            UseEvents = targetPlan.HasEvents,
            UseDirectory = targetPlan.HasDirectory,
            UseTotp = targetPlan.HasTotp,
            Use2fa = targetPlan.Has2fa,
            UseApi = targetPlan.HasApi,
            UseResetPassword = targetPlan.HasResetPassword,
            SelfHost = targetPlan.HasSelfHost,
            UsersGetPremium = targetPlan.UsersGetPremium || premiumAccess,
            UseCustomPermissions = targetPlan.HasCustomPermissions,
            UseScim = targetPlan.HasScim,
            Plan = targetPlan.Name,
            Gateway = null,
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
            Key = null, // Will need to be set by client
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
