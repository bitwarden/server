using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Vault.Entities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.RustSDK;
using Bit.Seeder.Data;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Shared mutable state bag passed through every <see cref="IStep"/> in a pipeline run.
/// WARNING: This class is NOT thread-safe. Each pipeline execution must use its own context instance.
/// Do not share a context instance between concurrent pipeline runs.
/// </summary>
/// <remarks>
/// <para>
/// The context holds mutable state that accumulates as steps execute:
/// <list type="bullet">
/// <item><description>Organization, Owner properties are set by early steps</description></item>
/// <item><description>Entity lists (Users, Ciphers, etc.) accumulate entities</description></item>
/// <item><description>Registry tracks entity IDs for cross-step references</description></item>
/// </list>
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
/// <strong>DatabaseContext Ownership:</strong>
/// The caller is responsible for managing the DatabaseContext lifetime.
/// This context does NOT take ownership or dispose the database connection.
/// </para>
/// <para>
/// Use the <c>Require*()</c> methods instead of accessing nullable properties directly —
/// they throw with step-ordering guidance if a prerequisite step hasn't run yet.
/// </para>
/// <example>
/// <code>
/// using var db = new DatabaseContext(connectionString);
/// var context = new SeederContext(db, passwordHasher, mangler, seedReader);
///
/// var recipe = new RecipeBuilder()
///     .WithStep(new CreateOrganizationStep("acme.com"))
///     .WithStep(new CreateOwnerStep())
///     .Build();
///
/// var executor = new RecipeExecutor();
/// await executor.Execute(recipe, context);
///
/// </code>
/// </example>
/// </remarks>
/// <param name="db">
/// DatabaseContext instance. CALLER IS RESPONSIBLE FOR DISPOSAL.
/// This context does not take ownership of the database connection.
/// </param>
/// <param name="passwordHasher">Service for hashing user passwords</param>
/// <param name="manglerService">Service for ID mangling in SeederApi scenarios</param>
/// <param name="seedReader">Service for reading embedded seed fixture JSON files</param>
/// <seealso cref="EntityRegistry"/>
/// <seealso cref="BulkCommitter"/>
internal sealed class SeederContext(
    DatabaseContext db,
    IPasswordHasher<User> passwordHasher,
    IManglerService manglerService,
    ISeedReader seedReader)
{
    internal DatabaseContext Db { get; } = db;

    internal IPasswordHasher<User> PasswordHasher { get; } = passwordHasher;

    internal IManglerService Mangler { get; } = manglerService;

    internal ISeedReader SeedReader { get; } = seedReader;

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
