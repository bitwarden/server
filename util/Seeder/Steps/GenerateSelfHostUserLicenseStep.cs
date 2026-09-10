using Bit.Core.Billing.Services;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Seeder.Steps;

/// <summary>
/// Writes a user premium license file to the LicenseDirectory.
/// Required for self-hosted instances, which validate premium status by reading this file on every login.
/// </summary>
internal sealed class GenerateSelfHostUserLicenseStep(
    ILicensingService licenseService,
    ISeederLicenseSigner licenseSigner,
    ILogger<GenerateSelfHostUserLicenseStep> logger) : IAsyncStep
{
    public async Task ExecuteAsync(SeederContext context)
    {
        var user = context.Owner;
        if (user is null || !user.Premium)
        {
            return;
        }

        // Outcome discarded: any warning is already logged.
        _ = await SelfHostLicenseService.WriteLicenseAsync(licenseService, licenseSigner, user, logger);
    }
}
