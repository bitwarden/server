using System.Globalization;
using System.Security.Claims;
using Bit.Core.Billing.Licenses.Extensions;
using Bit.Core.Billing.Licenses.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Billing.Licenses.Services.Implementations;

public class UserLicenseClaimsFactory : ILicenseClaimsFactory<User>
{
    public Task<List<Claim>> GenerateClaims(User entity, LicenseContext licenseContext)
    {
        var subscriptionInfo = licenseContext.SubscriptionInfo;

        var expires = entity.CalculateFreshExpirationDate(subscriptionInfo);
        var refresh = entity.CalculateFreshRefreshDate(subscriptionInfo);
        var trial = entity.IsTrialing(subscriptionInfo);

        var claims = new List<Claim>
        {
            new(nameof(UserLicenseConstants.LicenseType), LicenseType.User.ToString()),
            new(nameof(UserLicenseConstants.Id), entity.Id.ToString()),
            new(nameof(UserLicenseConstants.Premium), entity.Premium.ToString()),
            new(nameof(UserLicenseConstants.Issued), DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)),
            new(nameof(UserLicenseConstants.Trial), trial.ToString()),
        };

        if (entity.Email is not null)
        {
            claims.Add(new(nameof(UserLicenseConstants.Email), entity.Email));
        }

        if (entity.Name is not null)
        {
            claims.Add(new(nameof(UserLicenseConstants.Name), entity.Name));
        }

        if (entity.LicenseKey is not null)
        {
            claims.Add(new(nameof(UserLicenseConstants.LicenseKey), entity.LicenseKey));
        }

        if (entity.MaxStorageGb.HasValue)
        {
            claims.Add(new(nameof(UserLicenseConstants.MaxStorageGb), entity.MaxStorageGb.ToString()));
        }

        if (expires.HasValue)
        {
            claims.Add(new(nameof(UserLicenseConstants.Expires), expires.Value.ToString(CultureInfo.InvariantCulture)));
        }

        if (refresh.HasValue)
        {
            claims.Add(new(nameof(UserLicenseConstants.Refresh), refresh.Value.ToString(CultureInfo.InvariantCulture)));
        }

        return Task.FromResult(claims);
    }
}
