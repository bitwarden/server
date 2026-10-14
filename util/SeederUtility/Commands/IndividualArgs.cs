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

    [Option("email", Description = "Email for the user")]
    public string? Email { get; set; }

    [Option("vault", Description = "Generate ~75 personal ciphers and folders")]
    public bool Vault { get; set; } = false;

    [Option("password", Description = "Password for the seeded account (default: asdfasdfasdf)")]
    public string? Password { get; set; }

    [Option("kdf-iterations", Description = "KDF iteration count (default: 5000). Use 600000 for production-realistic e2e testing.")]
    public int KdfIterations { get; set; } = 5_000;

    [Option("self-hosted", Description = "Write a user license file to LicenseDirectory after seeding (required for self-hosted premium validation)")]
    public bool SelfHosted { get; set; }

    [Option("account-age-days", Description = "Backdate the account's CreationDate by N days (default: 0 = today)")]
    public int? AccountAgeDays { get; set; }

    [Option("mangle", Description = "Enable ID mangling for test isolation")]
    public bool Mangle { get; set; }

    [Option("stripe-billing", Description = "Create a real Stripe test-environment customer and premium subscription for the user. Requires a sk_test_ key and pricingUri; requires --subscription premium; not valid with --self-hosted.")]
    public bool StripeBilling { get; set; }

    [Option("stripe-card", Description = "Stripe test card: visa (default), mastercard, amex, fails-on-charge, or requires-authentication. Requires --stripe-billing.")]
    public string? StripeCard { get; set; }

    [Option("stripe-address", Description = "Billing address: us (default), us-alaska, de, or ph. de-vat is rejected: a personal account takes no tax ID. Requires --stripe-billing.")]
    public string? StripeAddress { get; set; }

    public void Validate()
    {
        var sub = Subscription?.ToLowerInvariant();
        if (sub is not ("free" or "premium"))
        {
            throw new ArgumentException("Subscription must be 'free' or 'premium'.");
        }

        if (StripeBilling && sub == "free")
        {
            throw new ArgumentException(
                "A free user has no Stripe subscription. Use --subscription premium or drop --stripe-billing.");
        }

        if (StripeBilling && SelfHosted)
        {
            throw new ArgumentException(
                "--stripe-billing is not supported with --self-hosted. The self-hosted license is signed before " +
                "billing runs, so it would carry the pre-Stripe expiry date.");
        }

        StripeBillingArgs.ValidateProfile(StripeBilling, StripeCard, StripeAddress);

        if (KdfIterations < 5_000)
        {
            throw new ArgumentException("KDF iterations must be at least 5,000.");
        }

        if (AccountAgeDays < 0)
        {
            throw new ArgumentException("Account age days must be >= 0.");
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
        Email = Email,
        Premium = string.Equals(Subscription, "premium", StringComparison.OrdinalIgnoreCase),
        GenerateVault = Vault,
        Password = Password,
        KdfIterations = KdfIterations,
        SelfHosted = SelfHosted,
        AccountAgeDays = AccountAgeDays ?? 0,
        StripeBilling = StripeBillingArgs.ToProfile(StripeBilling, StripeCard, StripeAddress)
    };
}
