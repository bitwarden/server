using Bit.Core.Vault.Enums;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Steps;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Fluent API for building seeding pipelines with validation of step dependencies.
/// </summary>
/// <remarks>
/// <para>
/// RecipeBuilder enforces critical rules:
/// <list type="number">
/// <item>Organization required (UseOrganization OR CreateOrganization)</item>
/// <item>Owner required (AddOwner)</item>
/// <item>Choose ONE user source (UseRoster XOR AddUsers)</item>
/// <item>Choose ONE cipher source (UseCiphers XOR AddCiphers)</item>
/// </list>
/// </para>
/// <para>
/// <strong>Phase Order:</strong> Org → Owner → Generator → Roster → Users → Groups → Collections → Ciphers
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var steps = new RecipeBuilder()
///     .CreateOrganization("Acme", "acme.com", 50)
///     .AddOwner()
///     .WithGenerator("acme.com")
///     .AddUsers(25)
///     .AddCollections(10)
///     .AddCiphers(100)
///     .Build();
/// </code>
/// </example>
internal sealed class RecipeBuilder
{
    private IStep? _orgStep;
    private IStep? _ownerStep;
    private IStep? _generatorStep;
    private IStep? _rosterStep;
    private IStep? _usersStep;
    private IStep? _groupsStep;
    private IStep? _collectionsStep;
    private IStep? _ciphersFixtureStep;
    private IStep? _ciphersGenerateStep;

    private bool _hasOrg;
    private bool _hasOwner;
    private bool _hasGenerator;
    private bool _hasRosterUsers;
    private bool _hasGeneratedUsers;

    /// <summary>
    /// Use an organization from embedded fixtures.
    /// </summary>
    /// <param name="fixture">Organization fixture name without extension</param>
    /// <returns>This builder for fluent chaining</returns>
    public RecipeBuilder UseOrganization(string fixture)
    {
        _orgStep = CreateOrganizationStep.FromFixture(fixture);
        _hasOrg = true;
        return this;
    }

    /// <summary>
    /// Use a roster from embedded fixtures (users, groups, collections).
    /// </summary>
    /// <param name="fixture">Roster fixture name without extension</param>
    /// <returns>This builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when AddUsers() was already called</exception>
    public RecipeBuilder UseRoster(string fixture)
    {
        if (_hasGeneratedUsers)
        {
            throw new InvalidOperationException(
                "Cannot call UseRoster() after AddUsers(). Choose one user source.");
        }

        _rosterStep = new CreateRosterStep(fixture);
        _hasRosterUsers = true;
        return this;
    }

    /// <summary>
    /// Use ciphers from embedded fixtures.
    /// </summary>
    /// <param name="fixture">Cipher fixture name without extension</param>
    /// <returns>This builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when AddCiphers() was already called</exception>
    public RecipeBuilder UseCiphers(string fixture)
    {
        if (_ciphersGenerateStep is not null)
        {
            throw new InvalidOperationException(
                "Cannot call UseCiphers() after AddCiphers(). Choose one cipher source.");
        }

        _ciphersFixtureStep = new CreateCiphersStep(fixture);
        return this;
    }

    /// <summary>
    /// Create an organization inline with specified parameters.
    /// </summary>
    /// <param name="name">Organization display name</param>
    /// <param name="domain">Organization domain (used for email generation)</param>
    /// <param name="seats">Number of user seats</param>
    /// <returns>This builder for fluent chaining</returns>
    public RecipeBuilder CreateOrganization(string name, string domain, int seats)
    {
        _orgStep = CreateOrganizationStep.FromParams(name, domain, seats);
        _hasOrg = true;
        return this;
    }

    /// <summary>
    /// Generate users with seeded random data.
    /// </summary>
    /// <param name="count">Number of users to generate</param>
    /// <param name="realisticStatusMix">If true, includes revoked/invited users; if false, all confirmed</param>
    /// <returns>This builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when UseRoster() was already called</exception>
    public RecipeBuilder AddUsers(int count, bool realisticStatusMix = false)
    {
        if (_hasRosterUsers)
        {
            throw new InvalidOperationException(
                "Cannot call AddUsers() after UseRoster(). Choose one user source.");
        }

        _usersStep = new CreateUsersStep(count, realisticStatusMix);
        _hasGeneratedUsers = true;
        return this;
    }

    /// <summary>
    /// Generate groups with random members from existing users.
    /// </summary>
    /// <param name="count">Number of groups to generate</param>
    /// <returns>This builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when no users exist</exception>
    public RecipeBuilder AddGroups(int count)
    {
        if (!_hasRosterUsers && !_hasGeneratedUsers)
        {
            throw new InvalidOperationException(
                "Groups require users. Call UseRoster() or AddUsers() first.");
        }

        _groupsStep = new CreateGroupsStep(count);
        return this;
    }

    /// <summary>
    /// Generate collections with random assignments.
    /// </summary>
    /// <param name="count">Number of collections to generate</param>
    /// <returns>This builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when no users exist</exception>
    public RecipeBuilder AddCollections(int count)
    {
        if (!_hasRosterUsers && !_hasGeneratedUsers)
        {
            throw new InvalidOperationException(
                "Collections require users. Call UseRoster() or AddUsers() first.");
        }

        _collectionsStep = CreateCollectionsStep.FromCount(count);
        return this;
    }

    /// <summary>
    /// Generate collections based on organizational structure model.
    /// </summary>
    /// <param name="structure">Organizational structure (Traditional, Spotify, Modern)</param>
    /// <returns>This builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when no users exist</exception>
    public RecipeBuilder AddCollections(OrgStructureModel structure)
    {
        if (!_hasRosterUsers && !_hasGeneratedUsers)
        {
            throw new InvalidOperationException(
                "Collections require users. Call UseRoster() or AddUsers() first.");
        }

        _collectionsStep = CreateCollectionsStep.FromStructure(structure);
        return this;
    }

    /// <summary>
    /// Generate ciphers with configurable type and password strength distributions.
    /// </summary>
    /// <param name="count">Number of ciphers to generate</param>
    /// <param name="typeDist">Distribution of cipher types. Uses realistic defaults if null.</param>
    /// <param name="pwDist">Distribution of password strengths. Uses realistic defaults if null.</param>
    /// <returns>This builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when UseCiphers() was already called</exception>
    public RecipeBuilder AddCiphers(
        int count,
        Distribution<CipherType>? typeDist = null,
        Distribution<PasswordStrength>? pwDist = null)
    {
        if (_ciphersFixtureStep is not null)
        {
            throw new InvalidOperationException(
                "Cannot call AddCiphers() after UseCiphers(). Choose one cipher source.");
        }

        _ciphersGenerateStep = new GenerateCiphersStep(count, typeDist, pwDist);
        return this;
    }

    /// <summary>
    /// Add an organization owner user with admin privileges.
    /// </summary>
    /// <returns>This builder for fluent chaining</returns>
    public RecipeBuilder AddOwner()
    {
        _ownerStep = new CreateOwnerStep();
        _hasOwner = true;
        return this;
    }

    /// <summary>
    /// Initialize seeded random generator for reproducible test data.
    /// </summary>
    /// <param name="domain">Organization domain (used for seeding randomness)</param>
    /// <param name="seed">Optional explicit seed. If null, domain hash is used.</param>
    /// <returns>This builder for fluent chaining</returns>
    public RecipeBuilder WithGenerator(string domain, int? seed = null)
    {
        _generatorStep = InitGeneratorStep.FromDomain(domain, seed);
        _hasGenerator = true;
        return this;
    }

    /// <summary>
    /// Builds the configured step list with validation of dependencies.
    /// </summary>
    /// <returns>Ordered list of steps ready for execution</returns>
    /// <exception cref="InvalidOperationException">Thrown when required steps missing or dependencies violated</exception>
    public IReadOnlyList<IStep> Build()
    {
        Validate();

        var steps = new List<IStep>();

        if (_orgStep is not null)
        {
            steps.Add(_orgStep);
        }

        if (_ownerStep is not null)
        {
            steps.Add(_ownerStep);
        }

        if (_generatorStep is not null)
        {
            steps.Add(_generatorStep);
        }

        if (_rosterStep is not null)
        {
            steps.Add(_rosterStep);
        }

        if (_usersStep is not null)
        {
            steps.Add(_usersStep);
        }

        if (_groupsStep is not null)
        {
            steps.Add(_groupsStep);
        }

        if (_collectionsStep is not null)
        {
            steps.Add(_collectionsStep);
        }

        if (_ciphersFixtureStep is not null)
        {
            steps.Add(_ciphersFixtureStep);
        }

        if (_ciphersGenerateStep is not null)
        {
            steps.Add(_ciphersGenerateStep);
        }

        return steps;
    }

    private void Validate()
    {
        // Only validate cross-step dependencies that require full builder state at Build() time
        if (!_hasOrg)
        {
            throw new InvalidOperationException(
                "Organization is required. Call UseOrganization() or CreateOrganization().");
        }

        if (!_hasOwner)
        {
            throw new InvalidOperationException(
                "Owner is required. Call AddOwner().");
        }

        if (_ciphersGenerateStep is not null && !_hasGenerator)
        {
            throw new InvalidOperationException(
                "Generated ciphers require a generator. Call WithGenerator() first.");
        }
    }
}
