using Bit.Core.Billing.Services;
using Bit.Core.Utilities;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;

namespace Bit.Seeder.Steps;

/// <summary>
/// Writes a user premium license file to the LicenseDirectory.
/// Required for self-hosted instances, which validate premium status by reading this file on every login.
/// Silently no-ops when licenseService is null or LicenseDirectory is not configured.
/// </summary>
internal sealed class GenerateSelfHostUserLicenseStep(
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
            SelfHostLicenseService.WriteLicenseAsync(licenseService, user).GetAwaiter().GetResult();
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"[GenerateSelfHostUserLicenseStep] Skipping premium user license write due to invalid operation: {ex.Message}");
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            Console.WriteLine($"[GenerateSelfHostUserLicenseStep] Skipping premium user license write due to cryptographic error: {ex.Message}");
        }
        catch (IOException ex)
        {
            Console.WriteLine($"[GenerateSelfHostUserLicenseStep] Skipping premium user license write due to I/O error: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"[GenerateSelfHostUserLicenseStep] Skipping premium user license write due to access error: {ex.Message}");
        }
    }
}
