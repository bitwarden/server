
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Data.Generators;
using Bit.Seeder.Helpers;
using Bit.Seeder.Options;

namespace Bit.Seeder.Data;

/// <summary>
/// Centralized context for all data generators in a seeding operation.
/// Lazy-initializes generators on first access to avoid creating unused instances.
/// </summary>
/// <remarks>
/// Adding a new generator:
///   1. Add private nullable field
///   2. Add public property with lazy initialization
///   3. Use in Recipe via _ctx.NewGenerator.Method()
/// </remarks>
internal sealed class GeneratorContext
{
    private readonly int _seed;

    private readonly GeographicRegion _region;

    private readonly OrganizationVaultOptions _options;

    private GeneratorContext(int seed, GeographicRegion region, OrganizationVaultOptions options)
    {
        _seed = seed;
        _region = region;
        _options = options;
    }

    /// <summary>
    /// Creates a GeneratorContext from vault options, deriving seed from domain if not specified.
    /// </summary>
    public static GeneratorContext FromOptions(OrganizationVaultOptions options)
    {
        var seed = options.Seed ?? StableHash.ToInt32(options.Domain);
        var region = options.Region ?? GeographicRegion.Global;
        return new GeneratorContext(seed, region, options);
    }

    /// <summary>
    /// The seed used for deterministic generation. Exposed for distribution calculations.
    /// </summary>
    public int Seed => _seed;

    /// <summary>
    /// Total cipher count from options. Used for distribution calculations.
    /// </summary>
    public int CipherCount => _options.Ciphers;

    private CipherUsernameGenerator? _username;

    public CipherUsernameGenerator Username => _username ??= new(
        _seed,
        _options.UsernameDistribution,
        _region,
        _options.UsernamePattern);

    private FolderNameGenerator? _folder;

    public FolderNameGenerator Folder => _folder ??= new(_seed);

    private CardDataGenerator? _card;

    public CardDataGenerator Card => _card ??= new(_seed, _region);

    private IdentityDataGenerator? _identity;

    public IdentityDataGenerator Identity => _identity ??= new(_seed, _region);

    private SecureNoteDataGenerator? _secureNote;

    public SecureNoteDataGenerator SecureNote => _secureNote ??= new(_seed);
}
