using System.Globalization;
using System.Security.Claims;
using Bit.Core.Billing.Licenses.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;

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
            new(nameof(UserLicenseConstants.LicenseType), LicenseType.User.ToString()),
            new(nameof(UserLicenseConstants.LicenseKey), entity.LicenseKey),
            new(nameof(UserLicenseConstants.Id), entity.Id.ToString()),
            new(nameof(UserLicenseConstants.Name), entity.Name),
            new(nameof(UserLicenseConstants.Email), entity.Email),
            new(nameof(UserLicenseConstants.Premium), entity.Premium.ToString()),
            new(nameof(UserLicenseConstants.MaxStorageGb), entity.MaxStorageGb.ToString()),
            new(nameof(UserLicenseConstants.Issued), DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)),
            new(nameof(UserLicenseConstants.Expires), expires.ToString()),
            new(nameof(UserLicenseConstants.Refresh), refresh.ToString()),
            new(nameof(UserLicenseConstants.Trial), trial.ToString()),
        };

        return Task.FromResult(claims);
    }
}
