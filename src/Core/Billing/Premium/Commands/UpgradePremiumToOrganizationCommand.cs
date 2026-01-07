using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
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
    /// <returns>A billing command result indicating success or failure with appropriate error details.</returns>
    Task<BillingCommandResult<None>> Run(
        User user,
        string organizationName,
        string key,
        PlanType targetPlanType);
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
        PlanType targetPlanType) => HandleAsync<None>(async () =>
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

        // Get the premium plan to identify which price IDs to delete
        var premiumPlan = await pricingClient.GetAvailablePremiumPlan();

        // Get the target organization plan
        var targetPlan = await pricingClient.GetPlanOrThrow(targetPlanType);

        // Build the list of subscription item updates
        var subscriptionItemOptions = new List<SubscriptionItemOptions>();

        // Mark existing Premium subscription items for deletion
        // Only delete Premium and storage items, not other potential subscription items
        foreach (var item in currentSubscription.Items.Data)
        {
            var priceId = item.Price.Id;
            var isPremiumItem = priceId == premiumPlan.Seat.StripePriceId;
            var isStorageItem = priceId == premiumPlan.Storage.StripePriceId;

            if (isPremiumItem || isStorageItem)
            {
                subscriptionItemOptions.Add(new SubscriptionItemOptions
                {
                    Id = item.Id,
                    Deleted = true
                });
            }
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

        // Generate organization ID early to include in metadata
        var organizationId = CoreHelpers.GenerateComb();

        // Build the subscription update options
        var subscriptionUpdateOptions = new SubscriptionUpdateOptions
        {
            Items = subscriptionItemOptions,
            ProrationBehavior = StripeConstants.ProrationBehavior.None,
            Metadata = new Dictionary<string, string>
            {
                [StripeConstants.MetadataKeys.OrganizationId] = organizationId.ToString(),
                [StripeConstants.MetadataKeys.PreviousPremiumPriceId] = premiumPlan.Seat.StripePriceId,
                [StripeConstants.MetadataKeys.PreviousPeriodEndDate] = currentSubscription.GetCurrentPeriodEnd()?.ToString("O") ?? string.Empty,
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
            Seats = (short)seats,
            MaxCollections = targetPlan.PasswordManager.MaxCollections,
            MaxStorageGb = targetPlan.PasswordManager.BaseStorageGb,
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
            UsersGetPremium = targetPlan.UsersGetPremium,
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
