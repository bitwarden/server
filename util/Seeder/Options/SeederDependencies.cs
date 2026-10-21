using AutoMapper;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Bit.Seeder.Options;

/// <summary>
/// Bundles the infrastructure services that all recipes require.
/// <see cref="LicensingService"/> is a factory because <c>LicensingService</c> throws on construction when
/// the configured licensing certificate cannot be loaded; only a signed self-hosted license resolves it.
/// </summary>
public sealed record SeederDependencies(
    DatabaseContext Db,
    IMapper Mapper,
    IPasswordHasher<User> PasswordHasher,
    IManglerService ManglerService,
    Func<ILicensingService> LicensingService,
    IAttachmentStorageService AttachmentStorageService,
    ISeederLicenseSigner LicenseSigner,
    ILoggerFactory LoggerFactory)
{
    /// <summary>
    /// Optional progress reporter. When null, the pipeline runs silently.
    /// Set via <c>with</c> expression from UI-facing callers (e.g., CLI).
    /// </summary>
    public IProgress<SeederProgressEvent>? Progress { get; init; }

    /// <summary>
    /// Optional factory for the organization Stripe billing service. Required only by callers that opt into
    /// Stripe billing; when null, any billing opt-in fails fast rather than seeding an org with no subscription.
    /// A factory rather than a resolved instance so the billing DI graph is only constructed when a
    /// command actually opts in, instead of on every command.
    /// </summary>
    public Func<IOrganizationStripeBillingService>? OrganizationStripeBillingService { get; init; }

    /// <summary>
    /// Optional factory for the user Stripe billing service. Required only by callers that opt an individual
    /// user into Stripe billing; when null, that opt-in fails fast. A factory for the same reason as
    /// <see cref="OrganizationStripeBillingService"/>.
    /// </summary>
    public Func<IUserStripeBillingService>? UserStripeBillingService { get; init; }

    /// <summary>
    /// When true, <c>User.Key</c> and <c>User.MasterPassword</c> are written without the <c>P|</c> data protection,
    /// so a server holding different data-protection keys can still read them.
    /// </summary>
    public bool DisableDataProtection { get; init; }
}
