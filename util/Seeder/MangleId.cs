using System.Globalization;

namespace Bit.Seeder;

/// <summary>
/// Helper for generating unique identifier suffixes to prevent collisions in test data.
/// "Mangling" adds a random suffix to test data identifiers (usernames, emails, org names, etc.)
/// to ensure uniqueness across multiple test runs and parallel test executions.
/// </summary>
public class MangleId
{
    public readonly string Value;

    public MangleId()
    {
        // Generate a short random string (6 char) to use as the mangle ID
        Value = Random.Shared.NextInt64().ToString("x", CultureInfo.InvariantCulture).Substring(0, 8);
    }

    public override string ToString() => Value;

    public string MangleEmail(string email) => $"{Value}+{email}";

    public static string ExtractDomain(string email)
    {
        var atIndex = email.IndexOf('@');
        return atIndex >= 0 ? email[(atIndex + 1)..] : "test.local";
    }
}
