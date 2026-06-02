using AutoMapper;
using Bit.Core.Entities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.Seeder.Options;

/// <summary>
/// Bundles the infrastructure services that all recipes require.
/// </summary>
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
    /// Optional outer service provider. When set, the orchestrator forwards selected services
    /// (logging + billing) from the outer scope into its per-recipe container so steps that
    /// need them (e.g. <see cref="Bit.Seeder.Steps.FinalizeOrganizationBillingStep"/>) can
    /// resolve their dependencies.
    /// </summary>
    public IServiceProvider? OuterServices { get; init; }

    /// <summary>
    /// When true, the pipeline skips Stripe finalization for paid-plan organizations.
    /// Useful for offline / air-gapped runs. Also honored if
    /// <c>BW_SEEDER_SKIP_STRIPE=true</c> is set in the environment, or if the global
    /// Stripe API key is missing.
    /// </summary>
    public bool SkipStripe { get; init; }
}
