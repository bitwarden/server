using Bit.Seeder.Data.Static;
using Bogus;

namespace Bit.Seeder.Data.Generators;

/// <summary>
/// Deterministic TOTP secrets for generated logins.
/// </summary>
internal sealed class TotpSecretGenerator(int seed)
{
    private static readonly ThreadLocal<Faker> _threadFaker = new(() => new Faker());

    /// <summary>
    /// Builds a 32-character base32 TOTP secret for the login at <paramref name="index"/>.
    /// </summary>
    internal string GenerateByIndex(int index)
    {
        var faker = _threadFaker.Value!;
        faker.Random = new Randomizer(seed + index);
        return faker.Random.String2(32, Alphabets.Base32);
    }
}
