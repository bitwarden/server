using Bit.Core.Billing.Licenses.Extensions;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Services;

namespace Bit.Core.Billing.Licenses.Queries.Implementations;

public class GetUserLicenseQuery : IGetUserLicenseQuery
{
    private readonly IPaymentService _paymentService;
    private readonly ILicensingService _licensingService;

    public GetUserLicenseQuery(
        IPaymentService paymentService,
        ILicensingService licensingService)
    {
        _paymentService = paymentService;
        _licensingService = licensingService;
    }

    public async Task<UserLicense> GetLicenseAsync(User user, SubscriptionInfo subscriptionInfo = null, int? version = null)
    {
        if (user == null)
        {
            throw new NotFoundException();
        }

        if (subscriptionInfo == null && user.Gateway != null)
        {
            subscriptionInfo = await _paymentService.GetSubscriptionAsync(user);
        }

        var issued = DateTime.UtcNow;

        var userLicense = new UserLicense
        {
            Version = version.GetValueOrDefault(1),
            LicenseType = LicenseType.User,
            LicenseKey = user.LicenseKey,
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Premium = user.Premium,
            MaxStorageGb = user.MaxStorageGb,
            Issued = issued,
            Expires = user.CalculateFreshExpirationDate(subscriptionInfo),
            Refresh = user.CalculateFreshRefreshDate(subscriptionInfo),
            Trial = user.IsTrialing(subscriptionInfo)
        };

        // Hash is included in Signature, and so must be initialized before signing
        userLicense.Hash = Convert.ToBase64String(userLicense.ComputeHash());
        userLicense.Signature = Convert.ToBase64String(_licensingService.SignLicense(userLicense));
        userLicense.Token = await _licensingService.CreateUserTokenAsync(user, subscriptionInfo);

        return userLicense;
    }
}
