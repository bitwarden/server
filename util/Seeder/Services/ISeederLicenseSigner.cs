using Bit.Core.Entities;

namespace Bit.Seeder.Services;

/// <summary>Outcome of a signing attempt.</summary>
public sealed record LicenseSigningResult(string? Token, string? Warning)
{
    public static LicenseSigningResult Signed(string token) => new(token, null);

    public static LicenseSigningResult Skipped(string warning) => new(null, warning);
}

/// <summary>
/// Signs self-hosted user premium license tokens with a private-key licensing certificate from configuration.
/// </summary>
public interface ISeederLicenseSigner
{
    /// <summary>
    /// Creates a signed license token. Returns a null token with a warning when no usable signing
    /// certificate is configured; callers treat that as "skip". Never throws for configuration problems.
    /// </summary>
    Task<LicenseSigningResult> CreateUserTokenAsync(User user);
}
