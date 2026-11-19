using System.Security.Cryptography;
using System.Text;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Data.Generators;
using Bit.Seeder.Options;

namespace Bit.Seeder.Data;

/// <summary>
/// Centralized context for all data generators in a seeding operation.
/// Lazy-initializes generators on first access to avoid creating unused instances.
/// </summary>
/// <remarks>
/// Adding a new generator:
///   1. Add a public property that creates the generator on first access with <c>field ??= new ...</c>
///   2. Use in Recipe via _ctx.NewGenerator.Method()
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
        var seed = options.Seed ?? DeriveStableSeed(options.Domain);
        var region = options.Region ?? GeographicRegion.Global;
        return new GeneratorContext(seed, region, options);
    }

    /// <summary>
    /// Derives a stable 32-bit seed from a domain string using SHA256.
    /// Same input always produces same output for deterministic generation.
    /// </summary>
    private static int DeriveStableSeed(string domain)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(domain));
        return BitConverter.ToInt32(bytes, 0);
    }

    /// <summary>
    /// The seed used for deterministic generation. Exposed for distribution calculations.
    /// </summary>
    public int Seed => _seed;

    public CipherUsernameGenerator Username => field ??= new CipherUsernameGenerator(
        _seed,
        _options.UsernameDistribution,
        _region,
        _options.UsernamePattern);

    public FolderNameGenerator Folder => field ??= new FolderNameGenerator(_seed);

    public CardDataGenerator Card => field ??= new CardDataGenerator(_seed, _region);

    public IdentityDataGenerator Identity => field ??= new IdentityDataGenerator(_seed, _region);

    public SecureNoteDataGenerator SecureNote => field ??= new SecureNoteDataGenerator(_seed);

    public UniquePasswordGenerator UniquePassword => field ??= new UniquePasswordGenerator(_seed);

    public LoginNotesGenerator LoginNotes => field ??= new LoginNotesGenerator(_seed);

    public CustomFieldGenerator CustomField => field ??= new CustomFieldGenerator(_seed);

    public TotpSecretGenerator TotpSecret => field ??= new TotpSecretGenerator(_seed);
}
