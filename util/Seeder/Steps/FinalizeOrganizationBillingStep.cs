using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.Services;
using Bit.Core.Billing.Pricing;
using Bit.Core.Models.Business;
using Bit.Core.Settings;
using Bit.Seeder.Options;
using Bit.Seeder.Pipeline;
using Microsoft.Extensions.Logging;

namespace Bit.Seeder.Steps;

/// <summary>
/// Creates a Stripe customer + trialing subscription for the seeded organization by reusing
/// the production sign-up helpers (<see cref="OrganizationSale.From"/> and
/// <see cref="IOrganizationBillingService.Finalize"/>).
/// </summary>
/// <remarks>
/// Runs as a post-commit step so the organization row already exists in the database when
/// <c>Finalize</c> persists Stripe IDs via <c>IOrganizationRepository.ReplaceAsync</c>.
/// Mirrors the Free-plan short-circuit in <c>CloudOrganizationSignUpCommand</c>; skips Stripe
/// entirely when the API key is missing or the opt-out flag is set so the seeder still works
/// offline.
/// </remarks>
internal sealed class FinalizeOrganizationBillingStep(
    IOrganizationBillingService organizationBillingService,
    IPricingClient pricingClient,
    IGlobalSettings globalSettings,
    SeederDependencies seederDependencies,
    ILogger<FinalizeOrganizationBillingStep> logger)
    : IAsyncStep, IPostCommitStep
{
    private const int TrialLengthDays = 30;

    public async Task ExecuteAsync(SeederContext context)
    {
        var organization = context.Organization;
        if (organization is null)
        {
            return;
        }

        if (organization.PlanType == PlanType.Free)
        {
            return;
        }

        if (ShouldSkipStripe(out var skipReason))
        {
            logger.LogWarning(
                "Seeder: skipping Stripe finalization for organization {OrgId} ({Reason}). Billing fields will remain null.",
                organization.Id, skipReason);
            return;
        }

        var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);

        // The seeder doesn't have access to the (async) pricing client in CreateOrganizationStep,
        // so it sets capacity fields without consulting plan baselines. Patch them here so the
        // org matches what CloudOrganizationSignUpCommand would produce — otherwise the admin
        // UI shows negative "additional" values (e.g. -50 machine accounts on Enterprise,
        // -6 seats on Teams Starter with --users 3).

        // Password Manager: clamp Seats and MaxStorageGb to at least the plan baseline. The
        // user's --users intent is preserved when it meets or exceeds the baseline; sub-baseline
        // values would put the org in a state production can't produce.
        organization.Seats = Math.Max(plan.PasswordManager.BaseSeats, organization.Seats ?? 0);
        if (plan.PasswordManager.BaseStorageGb > 0)
        {
            organization.MaxStorageGb = (short)Math.Max(plan.PasswordManager.BaseStorageGb, organization.MaxStorageGb ?? 0);
        }

        // Secrets Manager: store as (plan baseline + additional), matching
        // CloudOrganizationSignUpCommand.cs:111-115. The seeder never sets these directly,
        // so existing values are treated as additional (0 by default).
        if (organization.UseSecretsManager)
        {
            organization.SmSeats = plan.SecretsManager.BaseSeats + (organization.SmSeats ?? 0);
            organization.SmServiceAccounts = plan.SecretsManager.BaseServiceAccount + (organization.SmServiceAccounts ?? 0);
        }

        var signup = new OrganizationSignup
        {
            Name = organization.Name,
            BillingEmail = organization.BillingEmail,
            Plan = organization.PlanType,
            Owner = context.Owner,
            OwnerKey = string.Empty,
            CollectionName = string.Empty,
            AdditionalSeats = Math.Max(0, (organization.Seats ?? 0) - plan.PasswordManager.BaseSeats),
            AdditionalStorageGb = (short)Math.Max(0, (organization.MaxStorageGb ?? 0) - plan.PasswordManager.BaseStorageGb),
            PremiumAccessAddon = false,
            UseSecretsManager = organization.UseSecretsManager,
            AdditionalSmSeats = organization.UseSecretsManager
                ? Math.Max(0, (organization.SmSeats ?? 0) - plan.SecretsManager.BaseSeats)
                : null,
            AdditionalServiceAccounts = organization.UseSecretsManager
                ? Math.Max(0, (organization.SmServiceAccounts ?? 0) - plan.SecretsManager.BaseServiceAccount)
                : null,
            SkipTrial = false,
            TrialLength = TrialLengthDays,
            InitiationPath = "Seeder",
            IsFromProvider = false,
            IsFromSecretsManagerTrial = false,
        };

        var sale = OrganizationSale.From(organization, signup);
        await organizationBillingService.Finalize(sale);
    }

    private bool ShouldSkipStripe(out string reason)
    {
        if (string.IsNullOrWhiteSpace(globalSettings.Stripe?.ApiKey))
        {
            reason = "Stripe API key is not configured";
            return true;
        }

        if (string.Equals(Environment.GetEnvironmentVariable("BW_SEEDER_SKIP_STRIPE"), "true", StringComparison.OrdinalIgnoreCase))
        {
            reason = "BW_SEEDER_SKIP_STRIPE is set";
            return true;
        }

        if (seederDependencies.SkipStripe)
        {
            reason = "SeederDependencies.SkipStripe is true";
            return true;
        }

        reason = string.Empty;
        return false;
    }
}
