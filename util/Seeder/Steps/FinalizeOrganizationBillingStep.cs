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
/// offline. Plan-derived org capacity is applied earlier by
/// <see cref="CreateOrganizationStep"/>.
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

        var plan = context.Plan ?? await pricingClient.GetPlanOrThrow(organization.PlanType);

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
