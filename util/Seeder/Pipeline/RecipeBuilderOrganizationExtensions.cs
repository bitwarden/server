using Bit.Core.AdminConsole.Enums;
using Bit.Core.Auth.Enums;
using Bit.Core.Billing.Enums;
using Bit.Seeder.Options;
using Bit.Seeder.Services;
using Bit.Seeder.Steps;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Registers organization steps on a <see cref="RecipeBuilder"/>: the organization, its API key, policies, claimed
/// domains, SSO configuration and owner, and the organization's Stripe billing.
/// </summary>
public static class RecipeBuilderOrganizationExtensions
{
    extension(RecipeBuilder builder)
    {
        /// <summary>
        /// Use an organization from embedded fixtures with optional plan/seats/overrides from the preset.
        /// </summary>
        /// <param name="fixture">Organization fixture name without extension</param>
        /// <param name="planType">Optional plan type override (from preset)</param>
        /// <param name="seats">Optional seats override (from preset)</param>
        /// <param name="overrides">Optional org-level overrides applied on top of plan defaults. Null keeps all plan defaults.</param>
        /// <returns>The builder for fluent chaining</returns>
        public RecipeBuilder UseOrganization(
            string fixture,
            string? planType = null,
            int? seats = null,
            OrganizationOverrides? overrides = null)
        {
            builder.HasOrg = true;
            builder.AddStep(_ => CreateOrganizationStep.FromFixture(fixture, planType, seats, overrides));
            return builder;
        }

        /// <summary>
        /// Create an organization inline with specified parameters.
        /// </summary>
        /// <param name="name">Organization display name</param>
        /// <param name="domain">Organization domain (used for email generation)</param>
        /// <param name="seats">Number of user seats</param>
        /// <param name="planType">Billing plan type (defaults to EnterpriseAnnually)</param>
        /// <param name="overrides">Optional org-level overrides applied on top of plan defaults. Null keeps all plan defaults.</param>
        /// <returns>The builder for fluent chaining</returns>
        public RecipeBuilder CreateOrganization(
            string name,
            string domain,
            int? seats = null,
            PlanType planType = PlanType.EnterpriseAnnually,
            OrganizationOverrides? overrides = null)
        {
            builder.HasOrg = true;
            builder.AddStep(_ => CreateOrganizationStep.FromParams(name, domain, seats, planType, overrides));
            return builder;
        }

        /// <summary>
        /// Create a Default API key for the organization (required for OAuth client_credentials).
        /// </summary>
        /// <returns>The builder for fluent chaining</returns>
        public RecipeBuilder AddOrganizationApiKey()
        {
            if (!builder.HasOrg)
            {
                throw new InvalidOperationException(
                    "Organization API key requires an organization. Call UseOrganization() or CreateOrganization() first.");
            }

            builder.AddStep(_ => new CreateOrganizationApiKeyStep());
            return builder;
        }

        /// <summary>
        /// Create organization policy rows, enabled or disabled, with optional per-type Data JSON.
        /// </summary>
        /// <param name="policies">Policy type, enabled flag and Data JSON (null uses the step's default) per row</param>
        /// <returns>The builder for fluent chaining</returns>
        /// <exception cref="InvalidOperationException">Thrown when no organization exists</exception>
        internal RecipeBuilder AddPolicies(IReadOnlyList<(PolicyType Type, bool Enabled, string? Data)> policies)
        {
            if (!builder.HasOrg)
            {
                throw new InvalidOperationException(
                    "Policies require an organization. Call CreateOrganization() or UseOrganization() first.");
            }

            builder.AddStep(_ => new CreatePoliciesStep(policies));
            return builder;
        }

        /// <summary>
        /// Seed one or more claimed (verified) organization domains.
        /// Domain names are trimmed; empty entries are dropped and duplicates removed case-insensitively.
        /// </summary>
        /// <param name="domainNames">Domain names to seed. Empty (or all-empty/whitespace) is a no-op.</param>
        /// <returns>The builder for fluent chaining</returns>
        /// <exception cref="InvalidOperationException">Thrown when no organization exists.</exception>
        public RecipeBuilder WithOrganizationDomain(IReadOnlyList<string> domainNames)
        {
            if (!builder.HasOrg)
            {
                throw new InvalidOperationException(
                    "Organization domains require an organization. Call UseOrganization() or CreateOrganization() first.");
            }

            var unique = domainNames
                .Select(d => d.Trim())
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (unique.Count == 0)
            {
                return builder;
            }

            builder.AddStep(_ => new CreateOrganizationDomainsStep(unique));
            return builder;
        }

        /// <summary>
        /// Attach a SAML 2.0 SSO configuration (wired to the local dev IdP) and set the org's SSO identifier.
        /// Only SAML is supported today; other providers are skipped at execution time (see <see cref="CreateSsoConfigStep"/>).
        /// </summary>
        /// <param name="identifier">Org SSO identifier (the domain_hint typed at login); mangled with the org when --mangle is set</param>
        /// <param name="provider">Provider from the preset ("saml"/"oidc"); null defaults to saml</param>
        /// <param name="memberDecryptionType">How members decrypt after SSO auth (MasterPassword by default)</param>
        /// <returns>The builder for fluent chaining</returns>
        /// <exception cref="InvalidOperationException">Thrown when no organization exists or the identifier is empty</exception>
        public RecipeBuilder WithSso(
            string identifier,
            string? provider,
            MemberDecryptionType memberDecryptionType)
        {
            if (!builder.HasOrg)
            {
                throw new InvalidOperationException(
                    "SSO configuration requires an organization. Call UseOrganization() or CreateOrganization() first.");
            }

            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new InvalidOperationException("SSO configuration requires a non-empty identifier.");
            }

            builder.AddAsyncStep(_ => new CreateSsoConfigStep(identifier, provider, memberDecryptionType));
            return builder;
        }

        /// <summary>
        /// Add an organization owner user with admin privileges.
        /// </summary>
        /// <returns>The builder for fluent chaining</returns>
        public RecipeBuilder AddOwner()
        {
            builder.HasOwner = true;
            builder.AddStep(_ => new CreateOwnerStep());
            return builder;
        }

        /// <summary>
        /// Create a real Stripe test-environment customer and subscription for the organization after it commits.
        /// </summary>
        /// <param name="options">Trial configuration, test card, and billing address for the subscription</param>
        /// <returns>The builder for fluent chaining</returns>
        /// <remarks>
        /// Opt-in only. This step makes live network calls. <c>RecipeOrchestrator.ValidateBillingOptIn</c> still
        /// validates the host's Stripe configuration before any entity is created, for fast feedback — but
        /// <see cref="IOrganizationStripeBillingService.CreateSubscriptionAsync"/> also self-validates, so a
        /// caller that skips the pre-flight check fails before touching Stripe rather than after.
        /// </remarks>
        public RecipeBuilder WithStripeOrganizationBilling(StripeBillingOptions options)
        {
            builder.HasBilling = true;
            builder.AddAsyncStep(sp => new CreateOrganizationBillingStep(
                sp.GetRequiredService<IOrganizationStripeBillingService>(), options));
            return builder;
        }
    }
}
