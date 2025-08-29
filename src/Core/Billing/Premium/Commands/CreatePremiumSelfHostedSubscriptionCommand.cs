using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Platform.Push;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf.Types;

namespace Bit.Core.Billing.Premium.Commands;

public interface ICreatePremiumSelfHostedSubscriptionCommand
{
    Task<BillingCommandResult<None>> Run(User user, UserLicense license);
}

public class CreatePremiumSelfHostedSubscriptionCommand(
    ILicensingService licensingService,
    IUserService userService,
    IPushNotificationService pushNotificationService,
    ILogger<CreatePremiumSelfHostedSubscriptionCommand> logger)
    : BaseBillingCommand<CreatePremiumSelfHostedSubscriptionCommand>(logger), ICreatePremiumSelfHostedSubscriptionCommand
{
    public Task<BillingCommandResult<None>> Run(
        User user,
        UserLicense license) => HandleAsync<None>(async () =>
    {
        if (user.Premium)
        {
            return new BadRequest("Already a premium user.");
        }

        if (!licensingService.VerifyLicense(license))
        {
            return new BadRequest("Invalid license.");
        }

        var claimsPrincipal = licensingService.GetClaimsPrincipalFromLicense(license);
        if (!license.CanUse(user, claimsPrincipal, out var exceptionMessage))
        {
            return new BadRequest(exceptionMessage);
        }

        await licensingService.WriteUserLicenseAsync(user, license);

        user.Premium = true;
        user.RevisionDate = DateTime.UtcNow;
        user.MaxStorageGb = 10240; // 10 TB
        user.LicenseKey = license.LicenseKey;
        user.PremiumExpirationDate = license.Expires;

        await userService.SaveUserAsync(user);
        await pushNotificationService.PushSyncVaultAsync(user.Id);

        return new None();
    });
}
