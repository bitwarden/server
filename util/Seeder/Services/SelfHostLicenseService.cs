using System.Security.Cryptography;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Microsoft.Extensions.Logging;

namespace Bit.Seeder.Services;

/// <summary>Result of a premium license write; <see cref="Warning"/> explains why nothing was written.</summary>
internal readonly record struct LicenseWriteOutcome(bool Written, string? Warning)
{
    internal static readonly LicenseWriteOutcome Success = new(true, null);

    internal static LicenseWriteOutcome Skipped(string warning) => new(false, warning);
}

internal static class SelfHostLicenseService
{
    /// <summary>
    /// Best-effort premium license write. Without a private-key licensing certificate
    /// (licenseCertificatePath/licenseCertificatePassword) the signer returns a warning and nothing is
    /// written. Write failures are swallowed and returned as a warning so the write never aborts
    /// the caller.
    /// </summary>
    internal static async Task<LicenseWriteOutcome> WriteLicenseAsync(
        ILicensingService licenseService, ISeederLicenseSigner signer, User user, ILogger logger)
    {
        try
        {
            return await WriteLicenseCoreAsync(licenseService, signer, user);
        }
        catch (InvalidOperationException ex)
        {
            return Failed(logger, "invalid operation", ex);
        }
        catch (CryptographicException ex)
        {
            return Failed(logger, "cryptographic error", ex);
        }
        catch (IOException ex)
        {
            return Failed(logger, "I/O error", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Failed(logger, "access error", ex);
        }
    }

    private static async Task<LicenseWriteOutcome> WriteLicenseCoreAsync(
        ILicensingService licenseService, ISeederLicenseSigner signer, User user)
    {
        var signing = await signer.CreateUserTokenAsync(user);
        if (string.IsNullOrWhiteSpace(signing.Token))
        {
            return LicenseWriteOutcome.Skipped(
                signing.Warning ?? "No premium license was signed for this user.");
        }

        var license = new UserLicense
        {
            LicenseType = LicenseType.User,
            Id = user.Id,
            Email = user.Email,
            Name = user.Name,
            Premium = user.Premium,
            MaxStorageGb = user.MaxStorageGb,
            Issued = DateTime.UtcNow,
            Expires = user.PremiumExpirationDate?.AddDays(7),
            Version = 1,
            Token = signing.Token,
        };

        await licenseService.WriteUserLicenseAsync(user, license);

        return LicenseWriteOutcome.Success;
    }

    private static LicenseWriteOutcome Failed(ILogger logger, string reason, Exception ex)
    {
        logger.LogWarning(ex,
            "Premium user license write failed due to {Reason}. Skipping premium license generation.", reason);
        return LicenseWriteOutcome.Skipped($"Premium user license write failed due to {reason}. Skipping premium license generation.");
    }
}
