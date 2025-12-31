using Bit.Api.Billing.Attributes;
using Bit.Api.Billing.Models.Requests.Payment;
using Bit.Api.Billing.Models.Requests.Premium;
using Bit.Core;
using Bit.Core.Billing.Licenses.Queries;
using Bit.Core.Billing.Payment.Commands;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Premium.Commands;
using Bit.Core.Entities;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bit.Api.Billing.Controllers.VNext;

[Authorize("Application")]
[Route("account/billing/vnext")]
[SelfHosted(NotSelfHostedOnly = true)]
public class AccountBillingVNextController(
    ICreateBitPayInvoiceForCreditCommand createBitPayInvoiceForCreditCommand,
    ICreatePremiumCloudHostedSubscriptionCommand createPremiumCloudHostedSubscriptionCommand,
    IGetCreditQuery getCreditQuery,
    IGetPaymentMethodQuery getPaymentMethodQuery,
    IGetUserLicenseQuery getUserLicenseQuery,
    IUpdatePaymentMethodCommand updatePaymentMethodCommand,
    IUpgradePremiumToOrganizationCommand upgradePremiumToOrganizationCommand) : BaseBillingController
{
    [HttpGet("credit")]
    [InjectUser]
    public async Task<IResult> GetCreditAsync(
        [BindNever] User user)
    {
        var credit = await getCreditQuery.Run(user);
        return TypedResults.Ok(credit);
    }

    [HttpPost("credit/bitpay")]
    [InjectUser]
    public async Task<IResult> AddCreditViaBitPayAsync(
        [BindNever] User user,
        [FromBody] BitPayCreditRequest request)
    {
        var result = await createBitPayInvoiceForCreditCommand.Run(
            user,
            request.Amount,
            request.RedirectUrl);
        return Handle(result);
    }

    [HttpGet("payment-method")]
    [InjectUser]
    public async Task<IResult> GetPaymentMethodAsync(
        [BindNever] User user)
    {
        var paymentMethod = await getPaymentMethodQuery.Run(user);
        return TypedResults.Ok(paymentMethod);
    }

    [HttpPut("payment-method")]
    [InjectUser]
    public async Task<IResult> UpdatePaymentMethodAsync(
        [BindNever] User user,
        [FromBody] TokenizedPaymentMethodRequest request)
    {
        var (paymentMethod, billingAddress) = request.ToDomain();
        var result = await updatePaymentMethodCommand.Run(user, paymentMethod, billingAddress);
        return Handle(result);
    }

    [HttpPost("subscription")]
    [RequireFeature(FeatureFlagKeys.PM24996ImplementUpgradeFromFreeDialog)]
    [InjectUser]
    public async Task<IResult> CreateSubscriptionAsync(
        [BindNever] User user,
        [FromBody] PremiumCloudHostedSubscriptionRequest request)
    {
        var (paymentMethod, billingAddress, additionalStorageGb) = request.ToDomain();
        var result = await createPremiumCloudHostedSubscriptionCommand.Run(
            user, paymentMethod, billingAddress, additionalStorageGb);
        return Handle(result);
    }

    [HttpGet("license")]
    [InjectUser]
    public async Task<IResult> GetLicenseAsync(
        [BindNever] User user)
    {
        var response = await getUserLicenseQuery.Run(user);
        return TypedResults.Ok(response);
    }

    [HttpPost("upgrade")]
    [InjectUser]
    public async Task<IResult> UpgradePremiumToOrganizationAsync(
        [BindNever] User user,
        [FromBody] UpgradePremiumToOrganizationRequest request)
    {
        var (planType, seats, premiumAccess, storage, trialEndDate) = request.ToDomain();
        var result = await upgradePremiumToOrganizationCommand.Run(
            user, planType, seats, premiumAccess, storage, trialEndDate);
        return Handle(result);
    }
}
