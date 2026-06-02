using AutoMapper;
using Bit.Core.Billing.Organizations.Services;
using Bit.Core.Billing.Pricing;
using Bit.Core.Entities;
using Bit.Core.Settings;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Bit.Seeder.Options;

/// <summary>
/// Bundles the infrastructure services that all recipes require.
/// </summary>
/// <remarks>
/// The billing-related properties below are optional. They are required only when a recipe
/// includes <see cref="Steps.FinalizeOrganizationBillingStep"/> (i.e. any org preset). When
/// any of them is null the step will fail to construct at executor time with a missing-service
/// DI error — callers running org recipes are expected to populate all four.
/// </remarks>
public sealed record SeederDependencies(
    DatabaseContext Db,
    IMapper Mapper,
    IPasswordHasher<User> PasswordHasher,
    IManglerService ManglerService)
{
    /// <summary>
    /// Optional progress reporter. When null, the pipeline runs silently.
    /// Set via <c>with</c> expression from UI-facing callers (e.g., CLI).
    /// </summary>
    public IProgress<SeederProgressEvent>? Progress { get; init; }

    /// <summary>
    /// Production billing service. Required when running an org preset (the pipeline
    /// appends <see cref="Steps.FinalizeOrganizationBillingStep"/> automatically).
    /// </summary>
    public IOrganizationBillingService? OrganizationBillingService { get; init; }

    /// <summary>
    /// HTTP client for the Bitwarden Pricing Service. Required when running an org preset.
    /// </summary>
    public IPricingClient? PricingClient { get; init; }

    /// <summary>
    /// Global settings — read for the Stripe API key and pricing URI. Required when running
    /// an org preset.
    /// </summary>
    public IGlobalSettings? GlobalSettings { get; init; }

    /// <summary>
    /// Logger factory forwarded into the per-recipe DI container. Required when running an
    /// org preset (the billing step logs warnings on graceful-skip paths).
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; init; }

    /// <summary>
    /// When true, the pipeline skips Stripe finalization for paid-plan organizations.
    /// Useful for offline / air-gapped runs. Also honored if
    /// <c>BW_SEEDER_SKIP_STRIPE=true</c> is set in the environment, or if the global
    /// Stripe API key is missing.
    /// </summary>
    public bool SkipStripe { get; init; }
}
