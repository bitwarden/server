namespace Bit.Seeder.Options;

/// <summary>
/// Options for seeding a standalone individual user with optional personal vault data.
/// </summary>
public record IndividualUserOptions
{
    /// <summary>
    /// Optional first name. When both FirstName and LastName are set,
    /// the email is deterministic: "{first}.{last}@individual.example".
    /// When null, Faker generates a random name (requires mangling for isolation).
    /// </summary>
    public string? FirstName { get; init; }

    /// <summary>
    /// Optional last name. Must be provided together with FirstName.
    /// </summary>
    public string? LastName { get; init; }

    /// <summary>
    /// Optional email.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Whether the user has a premium subscription (enables 1 GB storage).
    /// </summary>
    public bool Premium { get; init; }

    /// <summary>
    /// When true, generates ~75 personal ciphers across 5 named folders.
    /// </summary>
    public bool GenerateVault { get; init; }

    /// <summary>
    /// Password for the seeded account. Defaults to "asdfasdfasdf" if not specified.
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    /// KDF iteration count. Defaults to 5,000 for fast seeding.
    /// Use 600,000 for production-realistic e2e testing.
    /// </summary>
    public int KdfIterations { get; init; } = 5_000;
}
