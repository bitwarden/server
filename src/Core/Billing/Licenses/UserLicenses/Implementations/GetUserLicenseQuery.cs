#nullable enable
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Services;

// ReSharper disable once CheckNamespace
namespace Bit.Core.Billing.Licenses.UserLicenses;

public class GetUserLicenseQuery
{
    public required User User { get; init; }
    public SubscriptionInfo? SubscriptionInfo { get; init; }
    public int? Version { get; init; }
}

public class GetUserLicenseQueryHandler(
    IPaymentService paymentService,
    ILicensingService licenseService)
    : IGetUserLicenseQueryHandler
{
    public async Task<UserLicense> Handle(GetUserLicenseQuery query)
    {
        if (query.User == null)
        {
            throw new NotFoundException();
        }

        var user = query.User;

        var subscriptionInfo = query.SubscriptionInfo is null && query.User.Gateway is null
            ? await paymentService.GetSubscriptionAsync(query.User)
            : query.SubscriptionInfo;

        var userLicense = new UserLicense
        {
            LicenseKey = user.LicenseKey,
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Version = query.Version.GetValueOrDefault(UserLicense.CurrentLicenseFileVersion + 1),
            Premium = user.Premium,
            MaxStorageGb = user.MaxStorageGb,
            Issued = DateTime.UtcNow,
            Expires = subscriptionInfo?.UpcomingInvoice?.Date?.AddDays(7) ??
                      user.PremiumExpirationDate?.AddDays(7),
            Refresh = subscriptionInfo?.UpcomingInvoice?.Date,
            Trial = (subscriptionInfo?.Subscription?.TrialEndDate.HasValue ?? false) &&
                    subscriptionInfo.Subscription.TrialEndDate.Value > DateTime.UtcNow
        };

        userLicense.Hash = Convert.ToBase64String(userLicense.EncodedHash);
        userLicense.Signature = Convert.ToBase64String(licenseService.SignLicense(userLicense));
        userLicense.Token = licenseService.GenerateToken(userLicense);

        return userLicense;
    }
}
