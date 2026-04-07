using Bit.Seeder.Options;
using CommandDotNet;

namespace Bit.SeederUtility.Commands;

/// <summary>
/// CLI argument model for the individual command.
/// Maps to <see cref="IndividualUserOptions"/> for the Seeder library.
/// </summary>
public class IndividualArgs : IArgumentModel
{
    [Option('s', "subscription", Description = "Subscription tier: free or premium")]
    public string Subscription { get; set; } = null!;

    [Option("first-name", Description = "First name for the user (generates predictable email)")]
    public string? FirstName { get; set; }

    [Option("last-name", Description = "Last name for the user (generates predictable email)")]
    public string? LastName { get; set; }

    [Option("vault", Description = "Generate ~75 personal ciphers and folders")]
    public bool Vault { get; set; } = false;

    [Option("password", Description = "Password for the seeded account (default: asdfasdfasdf)")]
    public string? Password { get; set; }

    [Option("kdf-iterations", Description = "KDF iteration count (default: 5000). Use 600000 for production-realistic e2e testing.")]
    public int KdfIterations { get; set; } = 5_000;

    [Option("mangle", Description = "Enable ID mangling for test isolation")]
    public bool Mangle { get; set; }

    public void Validate()
    {
        var sub = Subscription?.ToLowerInvariant();
        if (sub is not ("free" or "premium"))
        {
            throw new ArgumentException("Subscription must be 'free' or 'premium'.");
        }

        if (KdfIterations < 5_000)
        {
            throw new ArgumentException("KDF iterations must be at least 5,000.");
        }

        var hasFirst = !string.IsNullOrWhiteSpace(FirstName);
        var hasLast = !string.IsNullOrWhiteSpace(LastName);

        if (hasFirst != hasLast)
        {
            throw new ArgumentException("Provide both --first-name and --last-name, or neither.");
        }

        // No names → random Faker identity, auto-enable mangling for isolation
        if (!hasFirst)
        {
            Mangle = true;
        }
    }

    public IndividualUserOptions ToOptions() => new()
    {
        FirstName = FirstName,
        LastName = LastName,
        Premium = string.Equals(Subscription, "premium", StringComparison.OrdinalIgnoreCase),
        GenerateVault = Vault,
        Password = Password,
        KdfIterations = KdfIterations
    };
}
