using System.Globalization;

namespace Bit.Seeder;

/// <summary>
/// Helper for generating unique identifier suffixes to prevent collisions in test data.
/// "Mangling" adds a random suffix to test data identifiers (usernames, emails, org names, etc.)
/// to ensure uniqueness across multiple test runs and parallel test executions.
/// </summary>
public class MangleId
{
    public readonly string Value = Random.Shared.NextInt64().ToString("x", CultureInfo.InvariantCulture)[..8];

    public override string ToString() => Value;
}
