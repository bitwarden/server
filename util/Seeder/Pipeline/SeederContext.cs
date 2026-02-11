using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Vault.Entities;
using Bit.RustSDK;
using Bit.Seeder.Data;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Shared mutable state bag passed through every <see cref="IStep"/> in a pipeline run.
/// WARNING: This class is NOT thread-safe. Each pipeline execution must use its own context instance.
/// Do not share a context instance between concurrent pipeline runs.
/// </summary>
/// <remarks>
/// <para>
/// Steps resolve services from <see cref="Services"/> instead of accessing fixed properties.
/// Use the convenience extension methods in <see cref="SeederContextExtensions"/> for common services.
/// </para>
/// <para>
/// <strong>Context Lifecycle:</strong>
/// <list type="number">
/// <item><description>Create fresh context for each pipeline run</description></item>
/// <item><description>Pass to RecipeExecutor.Execute()</description></item>
/// <item><description>Steps mutate context progressively</description></item>
/// <item><description>BulkCommitter flushes entity lists to database</description></item>
/// <item><description>Return org ID from context</description></item>
/// <item><description>Discard context (do not reuse)</description></item>
/// </list>
/// </para>
/// <para>
/// Use the <c>Require*()</c> methods instead of accessing nullable properties directly —
/// they throw with step-ordering guidance if a prerequisite step hasn't run yet.
/// </para>
/// </remarks>
/// <param name="services">
/// Service provider for resolving dependencies. Steps access services via
/// <see cref="SeederContextExtensions"/> convenience methods.
/// </param>
/// <seealso cref="EntityRegistry"/>
/// <seealso cref="BulkCommitter"/>
internal sealed class SeederContext(IServiceProvider services)
{
    internal IServiceProvider Services { get; } = services;

    internal Organization? Organization { get; set; }

    internal OrganizationKeys? OrgKeys { get; set; }

    internal string? Domain { get; set; }

    internal User? Owner { get; set; }

    internal OrganizationUser? OwnerOrgUser { get; set; }

    internal List<Organization> Organizations { get; } = [];

    internal List<User> Users { get; } = [];

    internal List<OrganizationUser> OrganizationUsers { get; } = [];

    internal List<Cipher> Ciphers { get; } = [];

    internal List<Group> Groups { get; } = [];

    internal List<GroupUser> GroupUsers { get; } = [];

    internal List<Collection> Collections { get; } = [];

    internal List<CollectionUser> CollectionUsers { get; } = [];

    internal List<CollectionGroup> CollectionGroups { get; } = [];

    internal List<CollectionCipher> CollectionCiphers { get; } = [];

    internal EntityRegistry Registry { get; } = new();

    internal GeneratorContext? Generator { get; set; }

    internal Organization RequireOrganization() =>
        Organization ?? throw new InvalidOperationException("Organization not set. Run CreateOrganizationStep first.");

    internal string RequireOrgKey() =>
        OrgKeys?.Key ?? throw new InvalidOperationException("Organization keys not set. Run CreateOrganizationStep first.");

    internal Guid RequireOrgId() =>
        Organization?.Id ?? throw new InvalidOperationException("Organization not set. Run CreateOrganizationStep first.");

    internal string RequireDomain() =>
        Domain ?? throw new InvalidOperationException("Domain not set. Run CreateOrganizationStep first.");

    internal GeneratorContext RequireGenerator() =>
        Generator ?? throw new InvalidOperationException("Generator not set. Call WithGenerator() / InitGeneratorStep first.");
}
