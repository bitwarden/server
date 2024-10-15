using System.Security.Claims;
using Bit.Core.Billing.Licenses.UserLicenses;

// ReSharper disable once CheckNamespace
namespace Bit.Core.Billing.Licenses.ClaimsFactory;

public class UserLicenseClaimsFactory : ILicenseClaimsFactory<UserLicense>
{
    public Task<IEnumerable<Claim>> GenerateClaimsAsync(UserLicense context)
    {
        return Task.FromResult<IEnumerable<Claim>>([
            new Claim(nameof(UserLicense.LicenseKey), context.LicenseKey),
            new Claim(nameof(UserLicense.Id), context.Id.ToString()),
            new Claim(nameof(UserLicense.Name), context.Name),
            new Claim(nameof(UserLicense.Email), context.Email),
            new Claim(nameof(UserLicense.Premium), context.Premium.ToString()),
            new Claim(nameof(UserLicense.MaxStorageGb), context.MaxStorageGb.ToString()),
            new Claim(nameof(UserLicense.Version), context.Version.ToString()),
            new Claim(nameof(UserLicense.Issued), context.Issued.ToString()),
            new Claim(nameof(UserLicense.Refresh), context.Refresh.ToString()),
            new Claim(nameof(UserLicense.Expires), context.Expires.ToString()),
            new Claim(nameof(UserLicense.Trial), context.Trial.ToString()),
            new Claim(nameof(UserLicense.LicenseType), context.LicenseType.ToString())
        ]);
    }
}
