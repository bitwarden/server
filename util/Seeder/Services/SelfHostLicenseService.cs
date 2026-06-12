using Bit.Core.Billing.Models.Business;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Seeder.Services;

internal static class SelfHostLicenseService
{
    internal static async Task WriteLicenseAsync(ILicensingService licenseService, User user)
    {
        var token = await licenseService.CreateUserTokenAsync(user, null!);
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

        await licenseService.WriteUserLicenseAsync(user, license);
    }
}
