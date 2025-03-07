#nullable enable
using Bit.Api.Models.Request;
using Bit.Api.Models.Request.Accounts;
using Bit.Api.Models.Response;
using Bit.Api.Utilities;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

[Route("accounts")]
[Authorize("Application")]
public class AccountsController(
    IUserService userService) : Controller
{
    [HttpPost("premium")]
    public async Task<PaymentResponseModel> PostPremium(
        PremiumRequestModel model,
        [FromServices] GlobalSettings globalSettings)
    {
        var user = await userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var valid = model.Validate(globalSettings);
        UserLicense? license = null;
        if (valid && globalSettings.SelfHosted)
        {
            license = await ApiHelpers.ReadJsonFileFromBody<UserLicense>(HttpContext, model.License);
        }

        if (!valid && !globalSettings.SelfHosted && string.IsNullOrWhiteSpace(model.Country))
        {
            throw new BadRequestException("Country is required.");
        }

        if (!valid || (globalSettings.SelfHosted && license == null))
        {
            throw new BadRequestException("Invalid license.");
        }

        var result = await userService.SignUpPremiumAsync(user, model.PaymentToken,
            model.PaymentMethodType!.Value, model.AdditionalStorageGb.GetValueOrDefault(0), license,
            new TaxInfo { BillingAddressCountry = model.Country, BillingAddressPostalCode = model.PostalCode });

        var userTwoFactorEnabled = await userService.TwoFactorIsEnabledAsync(user);
        var userHasPremiumFromOrganization = await userService.HasPremiumFromOrganization(user);
        var organizationIdsManagingActiveUser = await GetOrganizationIdsManagingUserAsync(user.Id);

        var profile = new ProfileResponseModel(user, null, null, null, userTwoFactorEnabled,
            userHasPremiumFromOrganization, organizationIdsManagingActiveUser);
        return new PaymentResponseModel
        {
            UserProfile = profile,
            PaymentIntentClientSecret = result.Item2,
            Success = result.Item1
        };
    }

    [HttpGet("subscription")]
    public async Task<SubscriptionResponseModel> GetSubscription(
        [FromServices] GlobalSettings globalSettings,
        [FromServices] IPaymentService _paymentService)
    {
        var user = await userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        if (!globalSettings.SelfHosted && user.Gateway != null)
        {
            var subscriptionInfo = await _paymentService.GetSubscriptionAsync(user);
            var license = await userService.GenerateLicenseAsync(user, subscriptionInfo);
            return new SubscriptionResponseModel(user, subscriptionInfo, license);
        }
        else if (!globalSettings.SelfHosted)
        {
            var license = await userService.GenerateLicenseAsync(user);
            return new SubscriptionResponseModel(user, license);
        }
        else
        {
            return new SubscriptionResponseModel(user);
        }
    }

    [HttpPost("payment")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task PostPayment([FromBody] PaymentRequestModel model)
    {
        var user = await userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        await userService.ReplacePaymentMethodAsync(user, model.PaymentToken, model.PaymentMethodType!.Value,
            new TaxInfo
            {
                BillingAddressLine1 = model.Line1,
                BillingAddressLine2 = model.Line2,
                BillingAddressCity = model.City,
                BillingAddressState = model.State,
                BillingAddressCountry = model.Country,
                BillingAddressPostalCode = model.PostalCode,
                TaxIdNumber = model.TaxId
            });
    }

    [HttpPost("storage")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<PaymentResponseModel> PostStorage([FromBody] StorageRequestModel model)
    {
        var user = await userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var result = await userService.AdjustStorageAsync(user, model.StorageGbAdjustment!.Value);
        return new PaymentResponseModel { Success = true, PaymentIntentClientSecret = result };
    }



    [HttpPost("license")]
    [SelfHosted(SelfHostedOnly = true)]
    public async Task PostLicense(LicenseRequestModel model)
    {
        var user = await userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var license = await ApiHelpers.ReadJsonFileFromBody<UserLicense>(HttpContext, model.License);
        if (license == null)
        {
            throw new BadRequestException("Invalid license");
        }

        await userService.UpdateLicenseAsync(user, license);
    }

    [HttpPost("cancel")]
    public async Task PostCancel(
        [FromBody] SubscriptionCancellationRequestModel request,
        [FromServices] ICurrentContext currentContext,
        [FromServices] IReferenceEventService referenceEventService,
        [FromServices] ISubscriberService _subscriberService)
    {
        var user = await userService.GetUserByPrincipalAsync(User);

        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        await _subscriberService.CancelSubscription(user,
            new OffboardingSurveyResponse { UserId = user.Id, Reason = request.Reason, Feedback = request.Feedback },
            user.IsExpired());

        await referenceEventService.RaiseEventAsync(new ReferenceEvent(
            ReferenceEventType.CancelSubscription,
            user,
            currentContext)
        { EndOfPeriod = user.IsExpired() });
    }

    [HttpPost("reinstate-premium")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task PostReinstate()
    {
        var user = await userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        await userService.ReinstatePremiumAsync(user);
    }

    [HttpGet("tax")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<TaxInfoResponseModel> GetTaxInfo(
        [FromServices] IPaymentService _paymentService)
    {
        var user = await userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var taxInfo = await _paymentService.GetTaxInfoAsync(user);
        return new TaxInfoResponseModel(taxInfo);
    }

    [HttpPut("tax")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task PutTaxInfo(
        [FromBody] TaxInfoUpdateRequestModel model,
        [FromServices] IPaymentService _paymentService)
    {
        var user = await userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var taxInfo = new TaxInfo
        {
            BillingAddressPostalCode = model.PostalCode,
            BillingAddressCountry = model.Country,
        };
        await _paymentService.SaveTaxInfoAsync(user, taxInfo);
    }

    private async Task<IEnumerable<Guid>> GetOrganizationIdsManagingUserAsync(Guid userId)
    {
        var organizationManagingUser = await userService.GetOrganizationsManagingUserAsync(userId);
        return organizationManagingUser.Select(o => o.Id);
    }
}
