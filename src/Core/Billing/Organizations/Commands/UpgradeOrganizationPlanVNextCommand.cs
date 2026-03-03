using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.Services;
using Bit.Core.Billing.Pricing;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf.Types;

namespace Bit.Core.Billing.Organizations.Commands;

/// <summary>
/// Upgrades an organization's subscription plan by updating its Stripe subscription
/// and persisting the corresponding feature and configuration changes to the database.
/// </summary>
public interface IUpgradeOrganizationPlanVNextCommand
{
    /// <summary>
    /// Upgrades the <paramref name="organization"/> to the specified <paramref name="plan"/>
    /// by applying subscription changes through <see cref="IUpdateOrganizationSubscriptionCommand"/>
    /// and updating the organization's features, limits, and encryption keys.
    /// </summary>
    /// <param name="organization">The organization to upgrade.</param>
    /// <param name="plan">The target plan to upgrade to.</param>
    /// <param name="keys">Optional public key encryption key pair data to set during the upgrade.</param>
    /// <returns>
    /// A <see cref="BillingCommandResult{T}"/> containing <see cref="None"/> on success,
    /// or an error result if the subscription update or feature persistence fails.
    /// </returns>
    Task<BillingCommandResult<None>> Run(
        Organization organization,
        Plan plan,
        PublicKeyEncryptionKeyPairData? keys);
}

public class UpgradeOrganizationPlanVNextCommand(
    ILogger<UpgradeOrganizationPlanVNextCommand> logger,
    IOrganizationBillingService organizationBillingService,
    IOrganizationService organizationService,
    IPricingClient pricingClient,
    IUpdateOrganizationSubscriptionCommand updateOrganizationSubscriptionCommand) : BaseBillingCommand<UpgradeOrganizationPlanVNextCommand>(logger), IUpgradeOrganizationPlanVNextCommand
{
    protected override Conflict DefaultConflict => new("We had a problem upgrading your plan. Please contact support for assistance.");

    public Task<BillingCommandResult<None>> Run(
        Organization organization,
        Plan plan,
        PublicKeyEncryptionKeyPairData? keys) => HandleAsync(async () =>
    {
        var currentPlan = await pricingClient.GetPlanOrThrow(organization.PlanType);

        if (currentPlan.UpgradeSortOrder == plan.UpgradeSortOrder)
        {
            return new BadRequest("Your organization is already on this plan.");
        }

        if (currentPlan.UpgradeSortOrder > plan.UpgradeSortOrder)
        {
            return new BadRequest("You can't downgrade your organization's plan.");
        }

        if (string.IsNullOrEmpty(organization.GatewayCustomerId))
        {
            return new Conflict($"Organization's ({organization.Id}) Stripe customer should already have been created");
        }

        // Upgrade from Free
        if (currentPlan.Type == PlanType.Free && organization is
            {
                GatewaySubscriptionId: null,
                Seats: not null
            })
        {
            var sale = OrganizationSale.From(organization, new OrganizationUpgrade
            {
                Plan = plan.Type,
                AdditionalSeats = organization.Seats ?? 0,
                UseSecretsManager = organization.UseSecretsManager,
                AdditionalSmSeats = organization.UseSecretsManager ? organization.SmSeats : null,
            });

            await organizationBillingService.Finalize(sale);

            if (plan.HasNonSeatBasedPasswordManagerPlan())
            {
                organization.Seats = plan.PasswordManager.BaseSeats;
            }

            organization.MaxStorageGb = plan.PasswordManager.BaseStorageGb;

            if (organization.UseSecretsManager)
            {
                organization.SmServiceAccounts = plan.SecretsManager.BaseServiceAccount;
            }

            await UpdateOrganizationFeaturesAsync(organization, plan, keys);

            return new None();
        }

        var builder = OrganizationSubscriptionChangeSet.Builder();

        builder.ChangeItemPrice(
            GetPasswordManagerPriceId(currentPlan),
            GetPasswordManagerPriceId(plan));

        if (organization.MaxStorageGb > currentPlan.PasswordManager.BaseStorageGb)
        {
            builder.ChangeItemPrice(
                currentPlan.PasswordManager.StripeStoragePlanId,
                plan.PasswordManager.StripeStoragePlanId);
        }

        if (organization.UseSecretsManager)
        {
            builder.ChangeItemPrice(
                currentPlan.SecretsManager.StripeSeatPlanId,
                plan.SecretsManager.StripeSeatPlanId);

            if (organization.SmServiceAccounts > currentPlan.SecretsManager.BaseServiceAccount)
            {
                builder.ChangeItemPrice(
                    currentPlan.SecretsManager.StripeServiceAccountPlanId,
                    plan.SecretsManager.StripeServiceAccountPlanId);
            }
        }

        var changeSet = builder.Build();
        var result = await updateOrganizationSubscriptionCommand.Run(organization, changeSet);

        if (!result.Success)
        {
            return result.Map(_ => new None());
        }

        await UpdateOrganizationFeaturesAsync(organization, plan, keys);

        return result.Map(_ => new None());
    });

    private static string GetPasswordManagerPriceId(Plan plan) =>
        plan.HasNonSeatBasedPasswordManagerPlan()
            ? plan.PasswordManager.StripePlanId
            : plan.PasswordManager.StripeSeatPlanId;

    private async Task UpdateOrganizationFeaturesAsync(
        Organization organization,
        Plan plan,
        PublicKeyEncryptionKeyPairData? keys)
    {
        organization.Plan = plan.Name;
        organization.PlanType = plan.Type;
        organization.MaxCollections = plan.PasswordManager.MaxCollections;
        organization.UsePolicies = plan.HasPolicies;
        organization.UseSso = plan.HasSso;
        organization.UseKeyConnector = plan.HasKeyConnector;
        organization.UseScim = plan.HasScim;
        organization.UseGroups = plan.HasGroups;
        organization.UseDirectory = plan.HasDirectory;
        organization.UseEvents = plan.HasEvents;
        organization.UseTotp = plan.HasTotp;
        organization.Use2fa = plan.Has2fa;
        organization.UseApi = plan.HasApi;
        organization.UseResetPassword = plan.HasResetPassword;
        organization.SelfHost = plan.HasSelfHost;
        organization.UsersGetPremium = plan.UsersGetPremium;
        organization.UseCustomPermissions = plan.HasCustomPermissions;
        organization.UseOrganizationDomains = plan.HasOrganizationDomains;
        organization.UseAutomaticUserConfirmation = plan.AutomaticUserConfirmation;
        organization.UseMyItems = plan.HasMyItems;

        if (keys != null)
        {
            organization.BackfillPublicPrivateKeys(keys);
        }

        await organizationService.ReplaceAndUpdateCacheAsync(organization);
    }
}
