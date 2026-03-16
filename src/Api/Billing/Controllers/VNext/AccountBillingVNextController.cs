using Bit.Api.Billing.Attributes;
using Bit.Api.Billing.Models.Requests.Payment;
using Bit.Api.Billing.Models.Requests.Portal;
using Bit.Api.Billing.Models.Requests.Premium;
using Bit.Api.Billing.Models.Requests.Storage;
using Bit.Api.Billing.Models.Responses.Portal;
using Bit.Core;
using Bit.Core.Billing.Licenses.Queries;
using Bit.Core.Billing.Payment.Commands;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Portal.Commands;
using Bit.Core.Billing.Premium.Commands;
using Bit.Core.Billing.Subscriptions.Commands;
using Bit.Core.Billing.Subscriptions.Queries;
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
    ICreateBillingPortalSessionCommand createBillingPortalSessionCommand,
    ICreateBitPayInvoiceForCreditCommand createBitPayInvoiceForCreditCommand,
    ICreatePremiumCloudHostedSubscriptionCommand createPremiumCloudHostedSubscriptionCommand,
    IGetBitwardenSubscriptionQuery getBitwardenSubscriptionQuery,
    IGetCreditQuery getCreditQuery,
    IGetPaymentMethodQuery getPaymentMethodQuery,
    IGetUserLicenseQuery getUserLicenseQuery,
    IReinstateSubscriptionCommand reinstateSubscriptionCommand,
    IUpdatePaymentMethodCommand updatePaymentMethodCommand,
    IUpdatePremiumStorageCommand updatePremiumStorageCommand,
    IUpgradePremiumToOrganizationCommand upgradePremiumToOrganizationCommand,
    IGetApplicableDiscountsQuery getApplicableDiscountsQuery) : BaseBillingController
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
    [InjectUser]
    public async Task<IResult> CreateSubscriptionAsync(
        [BindNever] User user,
        [FromBody] PremiumCloudHostedSubscriptionRequest request)
    {
        var subscriptionPurchase = request.ToDomain();
        var result = await createPremiumCloudHostedSubscriptionCommand.Run(user, subscriptionPurchase);
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

    [HttpGet("subscription")]
    [RequireFeature(FeatureFlagKeys.PM29594_UpdateIndividualSubscriptionPage)]
    [InjectUser]
    public async Task<IResult> GetSubscriptionAsync(
        [BindNever] User user)
    {
        var subscription = await getBitwardenSubscriptionQuery.Run(user);
        return subscription == null ? TypedResults.NotFound() : TypedResults.Ok(subscription);
    }

    [HttpPost("subscription/reinstate")]
    [RequireFeature(FeatureFlagKeys.PM29594_UpdateIndividualSubscriptionPage)]
    [InjectUser]
    public async Task<IResult> ReinstateSubscriptionAsync(
        [BindNever] User user)
    {
        var result = await reinstateSubscriptionCommand.Run(user);
        return Handle(result);
    }

    [HttpPut("subscription/storage")]
    [RequireFeature(FeatureFlagKeys.PM29594_UpdateIndividualSubscriptionPage)]
    [InjectUser]
    public async Task<IResult> UpdateSubscriptionStorageAsync(
        [BindNever] User user,
        [FromBody] StorageUpdateRequest request)
    {
        var result = await updatePremiumStorageCommand.Run(user, request.AdditionalStorageGb);
        return Handle(result);
    }

    [HttpPost("upgrade")]
    [InjectUser]
    public async Task<IResult> UpgradePremiumToOrganizationAsync(
        [BindNever] User user,
        [FromBody] UpgradePremiumToOrganizationRequest request)
    {
        var (organizationName, key, publicKey, encryptedPrivateKey, collectionName, planType, billingAddress) = request.ToDomain();
        var result = await upgradePremiumToOrganizationCommand.Run(user, organizationName, key, publicKey, encryptedPrivateKey, collectionName, planType, billingAddress);
        return Handle(result);
    }

    [HttpGet("discounts")]
    [RequireFeature(FeatureFlagKeys.PM29108_EnablePersonalDiscounts)]
    [InjectUser]
    public async Task<IResult> GetApplicableDiscountsAsync(
        [BindNever] User user)
    {
        var result = await getApplicableDiscountsQuery.Run(user);
        return Handle(result);
    }

    /// <summary>
    /// Creates a Stripe billing portal session for the authenticated user.
    /// The portal allows users to manage their subscription, payment methods, and billing history.
    /// </summary>
    /// <param name="user">The authenticated user</param>
    /// <param name="request">Portal session configuration including return URL</param>
    /// <returns>Portal session URL for redirection</returns>
    [HttpPost("portal-session")]
    [InjectUser]
    public async Task<IResult> CreatePortalSessionAsync(
        [BindNever] User user,
        [FromBody] PortalSessionRequest request)
    {
        var result = await createBillingPortalSessionCommand.Run(user, request.ReturnUrl!);
        return Handle(result.Map(url => new PortalSessionResponse { Url = url }));
    }

}
