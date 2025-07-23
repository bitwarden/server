#nullable enable
using Bit.Api.Billing.Attributes;
using Bit.Api.Billing.Models.Requests.Payment;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Billing.Payment.Commands;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
// ReSharper disable RouteTemplates.MethodMissingRouteParameters

namespace Bit.Api.Billing.Controllers.VNext;

[Route("providers/{providerId:guid}/billing/vnext")]
[SelfHosted(NotSelfHostedOnly = true)]
public class ProviderBillingVNextController(
    ICreateBitPayInvoiceForCreditCommand createBitPayInvoiceForCreditCommand,
    IGetBillingAddressQuery getBillingAddressQuery,
    IGetCreditQuery getCreditQuery,
    IGetPaymentMethodQuery getPaymentMethodQuery,
    IUpdateBillingAddressCommand updateBillingAddressCommand,
    IUpdatePaymentMethodCommand updatePaymentMethodCommand,
    IVerifyBankAccountCommand verifyBankAccountCommand) : BaseBillingController
{
    [HttpGet("address")]
    [InjectProvider(ProviderUserType.ProviderAdmin)]
    public async Task<IResult> GetBillingAddressAsync(
        [BindNever] Provider provider)
    {
        var billingAddress = await getBillingAddressQuery.Run(provider);
        return TypedResults.Ok(billingAddress);
    }

    [HttpPut("address")]
    [InjectProvider(ProviderUserType.ProviderAdmin)]
    public async Task<IResult> UpdateBillingAddressAsync(
        [BindNever] Provider provider,
        [FromBody] BillingAddressRequest request)
    {
        var billingAddress = request.ToDomain();
        var result = await updateBillingAddressCommand.Run(provider, billingAddress);
        return Handle(result);
    }

    [HttpGet("credit")]
    [InjectProvider(ProviderUserType.ProviderAdmin)]
    public async Task<IResult> GetCreditAsync(
        [BindNever] Provider provider)
    {
        var credit = await getCreditQuery.Run(provider);
        return TypedResults.Ok(credit);
    }

    [HttpPost("credit/bitpay")]
    [InjectProvider(ProviderUserType.ProviderAdmin)]
    public async Task<IResult> AddCreditViaBitPayAsync(
        [BindNever] Provider provider,
        [FromBody] BitPayCreditRequest request)
    {
        var result = await createBitPayInvoiceForCreditCommand.Run(
            provider,
            request.Amount,
            request.RedirectUrl);
        return Handle(result);
    }

    [HttpGet("payment-method")]
    [InjectProvider(ProviderUserType.ProviderAdmin)]
    public async Task<IResult> GetPaymentMethodAsync(
        [BindNever] Provider provider)
    {
        var paymentMethod = await getPaymentMethodQuery.Run(provider);
        return TypedResults.Ok(paymentMethod);
    }

    [HttpPut("payment-method")]
    [InjectProvider(ProviderUserType.ProviderAdmin)]
    public async Task<IResult> UpdatePaymentMethodAsync(
        [BindNever] Provider provider,
        [FromBody] TokenizedPaymentMethodRequest request)
    {
        var (paymentMethod, billingAddress) = request.ToDomain();
        var result = await updatePaymentMethodCommand.Run(provider, paymentMethod, billingAddress);
        return Handle(result);
    }

    [HttpPost("payment-method/verify-bank-account")]
    [InjectProvider(ProviderUserType.ProviderAdmin)]
    public async Task<IResult> VerifyBankAccountAsync(
        [BindNever] Provider provider,
        [FromBody] VerifyBankAccountRequest request)
    {
        var result = await verifyBankAccountCommand.Run(provider, request.DescriptorCode);
        return Handle(result);
    }
}
