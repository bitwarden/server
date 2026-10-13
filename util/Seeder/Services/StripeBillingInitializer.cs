using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.Services;
using Bit.Core.Billing.Pricing;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.Settings;
using Bit.Seeder.Options;

namespace Bit.Seeder.Services;

/// <summary>
/// Drives the production billing path (<see cref="OrganizationSale.From"/> →
/// <see cref="IOrganizationBillingService.Finalize"/>) against the shared Stripe <c>sk_test_</c> account.
/// </summary>
/// <remarks>
/// Reusing the Core services is deliberate rather than convenient: <c>OrganizationSale</c>'s constructor is
/// internal to <c>Bit.Core</c>, so <c>From</c> is the only way to build one, and going through it means seeded
/// orgs get the same customer, subscription, and trial shape a real signup does.
/// </remarks>
public sealed class StripeBillingInitializer(
    GlobalSettings globalSettings,
    IOrganizationBillingService organizationBillingService,
    IPricingClient pricingClient) : IStripeBillingInitializer
{
    /// <summary>Only a test-mode secret key is ever acceptable — this tool must not touch live billing.</summary>
    public const string TestKeyPrefix = "sk_test_";

    public void ValidateConfiguration(PlanType planType)
    {
        var apiKey = globalSettings.Stripe?.ApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Stripe billing was requested but no Stripe API key is configured. Set " +
                $"'globalSettings:stripe:apiKey' to a test-mode key ('{TestKeyPrefix}…') in the " +
                "'bitwarden-seeder-utility' user secrets.");
        }

        if (!apiKey.StartsWith(TestKeyPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Stripe billing requires a test-mode API key starting with '{TestKeyPrefix}'. The configured " +
                "key is not one, and the Seeder refuses to create billing against a live Stripe account.");
        }

        if (string.IsNullOrWhiteSpace(globalSettings.PricingUri))
        {
            throw new InvalidOperationException(
                "Stripe billing requires 'globalSettings:pricingUri' so plan pricing can be resolved. " +
                "Run the CLI with ASPNETCORE_ENVIRONMENT=Development to pick up " +
                "'util/SeederUtility/appsettings.Development.json', or set the value in user secrets.");
        }

        if (globalSettings.SelfHosted)
        {
            throw new InvalidOperationException(
                "Stripe billing requires cloud mode, but 'globalSettings:selfHosted' is true — the Pricing " +
                "Service is never called in self-hosted mode, so no plan can be resolved. Run this command " +
                "with 'globalSettings__selfHosted=false' in the environment.");
        }

        if (planType == PlanType.Free)
        {
            throw new InvalidOperationException(
                "The Free plan has no Stripe subscription, so it cannot be seeded with Stripe billing. " +
                "Choose a paid plan (e.g. teams-monthly, enterprise-annually) or drop the billing opt-in.");
        }
    }

    public async Task InitializeOrganizationAsync(Organization organization, StripeBillingOptions options)
    {
        ValidateConfiguration(organization.PlanType);

        try
        {
            var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);
            var signup = BuildSignup(organization, plan, options);

            await organizationBillingService.Finalize(OrganizationSale.From(organization, signup));
        }
        catch (Exception ex) when (ex is Stripe.StripeException or BillingException or BadRequestException
            or NotFoundException or HttpRequestException)
        {
            throw new InvalidOperationException(
                $"Stripe billing failed for organization '{organization.Id}', which was already committed to the " +
                $"database. Gateway customer: {organization.GatewayCustomerId ?? "<none>"}, subscription: " +
                $"{organization.GatewaySubscriptionId ?? "<none>"}. Cancel any orphaned Stripe objects before " +
                $"re-seeding: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Translates the already-seeded organization back into the signup shape the billing services expect.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Seat and storage quantities on <see cref="OrganizationSignup"/> are <em>additional</em> amounts on top
    /// of what the plan already bundles, while the seeded organization records absolute totals — hence the
    /// back-computation, clamped at zero so a plan whose base exceeds the seeded total never bills negative.
    /// </para>
    /// <para>
    /// Secrets Manager is included only when <see cref="Organization.SmSeats"/> has a value. Seeded Teams and
    /// Enterprise orgs carry <c>UseSecretsManager = true</c> with <c>SmSeats</c> NULL (the plan sets the flag;
    /// provisioning sets the seats), and inventing a quantity from NULL would bill for seats nobody asked for.
    /// The consequence is a deliberate mismatch: such an org keeps <c>UseSecretsManager = 1</c> in the
    /// database while its Stripe subscription has no Secrets Manager items.
    /// </para>
    /// </remarks>
    internal static OrganizationSignup BuildSignup(Organization organization, Plan plan, StripeBillingOptions options)
    {
        var signup = new OrganizationSignup
        {
            Plan = organization.PlanType,
            AdditionalSeats = AdditionalOver(organization.Seats, plan.PasswordManager.BaseSeats),
            AdditionalStorageGb = (short)AdditionalOver(organization.MaxStorageGb, plan.PasswordManager.BaseStorageGb),
            PaymentMethodType = PaymentMethodType.Card,
            PaymentToken = "pm_card_visa",
            TaxInfo = new TaxInfo
            {
                BillingAddressCountry = "US",
                BillingAddressPostalCode = "43432",
            },
            InitiationPath = "Seeder",
            SkipTrial = options.SkipTrial,
            TrialLength = options.SkipTrial ? null : options.TrialDays,
        };

        if (organization.SmSeats.HasValue && plan.SecretsManager is not null)
        {
            signup.UseSecretsManager = true;
            signup.AdditionalSmSeats = AdditionalOver(organization.SmSeats, plan.SecretsManager.BaseSeats);
            signup.AdditionalServiceAccounts =
                AdditionalOver(organization.SmServiceAccounts, plan.SecretsManager.BaseServiceAccount);
        }

        return signup;
    }

    private static int AdditionalOver(int? total, int baseIncluded) => Math.Max(0, (total ?? 0) - baseIncluded);
}
