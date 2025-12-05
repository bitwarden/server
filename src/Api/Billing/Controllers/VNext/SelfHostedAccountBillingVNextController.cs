using Bit.Api.Billing.Attributes;
using Bit.Api.Billing.Models.Requests.Premium;
using Bit.Api.Utilities;
using Bit.Core;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Billing.Premium.Commands;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bit.Api.Billing.Controllers.VNext;

[Authorize("Application")]
[Route("account/billing/vnext/self-host")]
[SelfHosted(SelfHostedOnly = true)]
public class SelfHostedAccountBillingVNextController(
    ICreatePremiumSelfHostedSubscriptionCommand createPremiumSelfHostedSubscriptionCommand) : BaseBillingController
{
    [HttpPost("license")]
    [RequireFeature(FeatureFlagKeys.PM24996ImplementUpgradeFromFreeDialog)]
    [InjectUser]
    public async Task<IResult> UploadLicenseAsync(
        [BindNever] User user,
        PremiumSelfHostedSubscriptionRequest request)
    {
        var license = await ApiHelpers.ReadJsonFileFromBody<UserLicense>(HttpContext, request.License);
        if (license == null)
        {
            throw new BadRequestException("Invalid license.");
        }
        var result = await createPremiumSelfHostedSubscriptionCommand.Run(user, license);
        return Handle(result);
    }
}
