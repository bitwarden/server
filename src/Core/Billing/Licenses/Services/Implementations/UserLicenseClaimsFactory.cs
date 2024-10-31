using System.Globalization;
using System.Security.Claims;
using Bit.Core.Billing.Licenses.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;

namespace Bit.Core.Billing.Licenses.Services.Implementations;

public class UserLicenseClaimsFactory : ILicenseClaimsFactory<User>
{
    public Task<List<Claim>> GenerateClaims(User entity, LicenseContext licenseContext)
    {
        var subscriptionInfo = licenseContext.SubscriptionInfo;

        var expires = subscriptionInfo.UpcomingInvoice?.Date?.AddDays(7) ?? entity.PremiumExpirationDate?.AddDays(7);
        var refresh = subscriptionInfo.UpcomingInvoice?.Date ?? entity.PremiumExpirationDate;
        var trial = (subscriptionInfo.Subscription?.TrialEndDate.HasValue ?? false) &&
                    subscriptionInfo.Subscription.TrialEndDate.Value > DateTime.UtcNow;

        var claims = new List<Claim>
        {
            new(nameof(UserLicense.LicenseType), LicenseType.User.ToString()),
            new(nameof(UserLicense.LicenseKey), entity.LicenseKey),
            new(nameof(UserLicense.Id), entity.Id.ToString()),
            new(nameof(UserLicense.Name), entity.Name),
            new(nameof(UserLicense.Email), entity.Email),
            new(nameof(UserLicense.Premium), entity.Premium.ToString()),
            new(nameof(UserLicense.MaxStorageGb), entity.MaxStorageGb.ToString()),
            new(nameof(UserLicense.Issued), DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)),
            new(nameof(UserLicense.Expires), expires.ToString()),
            new(nameof(UserLicense.Refresh), refresh.ToString()),
            new(nameof(UserLicense.Trial), trial.ToString()),
        };

        return Task.FromResult(claims);
    }
}
