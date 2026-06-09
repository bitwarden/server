using Bit.Core.Billing.Models.Business;
using Bit.Core.Billing.Services;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Writes a user premium license file to the LicenseDirectory.
/// Required for self-hosted instances, which validate premium status by reading this file on every login.
/// Silently no-ops when licenseService is null or LicenseDirectory is not configured.
/// </summary>
internal sealed class WritePremiumUserLicenseStep(
    ILicensingService? licenseService,
    string licenseDirectory) : IStep
{
    public void Execute(SeederContext context)
    {
        if (licenseService is null)
        {
            return;
        }

        if (!CoreHelpers.SettingHasValue(licenseDirectory))
        {
            return;
        }

        var user = context.Owner;
        if (user is null || !user.Premium)
        {
            return;
        }

        // Best-effort license write. Self-hosted instances hold only the public licensing
        // certificate, so token signing throws there (by design — see LicensingService.SignLicense).
        // Don't let that failure abort the pipeline run.
        try
        {
            var token = licenseService.CreateUserTokenAsync(user, null!).GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
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
                Token = token,
            };

            licenseService.WriteUserLicenseAsync(user, license).GetAwaiter().GetResult();
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"[WritePremiumUserLicenseStep] Skipping premium user license write due to invalid operation: {ex.Message}");
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            Console.WriteLine($"[WritePremiumUserLicenseStep] Skipping premium user license write due to cryptographic error: {ex.Message}");
        }
    }
}
