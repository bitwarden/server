using Bit.Api.Billing.Models.Requests.Payment;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Payment.Commands;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Context;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers.VNext;

[Route("providers/{providerId:guid}/billing/vnext")]
[SelfHosted(NotSelfHostedOnly = true)]
public class ProviderBillingVNextController(
    ICurrentContext currentContext,
    ICreateBitPayInvoiceForCreditCommand createBitPayInvoiceForCreditCommand,
    IGetBillingAddressQuery getBillingAddressQuery,
    IGetCreditQuery getCreditQuery,
    IGetPaymentMethodQuery getPaymentMethodQuery,
    IProviderRepository providerRepository,
    IUpdateBillingAddressCommand updateBillingAddressCommand,
    IUpdatePaymentMethodCommand updatePaymentMethodCommand,
    IVerifyBankAccountCommand verifyBankAccountCommand) : BaseBillingController
{
    [HttpGet("address")]
    public Task<IResult> GetBillingAddressAsync(
        [FromRoute] Guid providerId)
        => IfProviderAdminAsync(
            providerId,
            async provider =>
            {
                var billingAddress = await getBillingAddressQuery.Run(provider);
                return TypedResults.Ok(billingAddress);
            });

    [HttpPut("address")]
    public Task<IResult> UpdateBillingAddressAsync(
        [FromRoute] Guid providerId,
        [FromBody] BillingAddressRequest request)
        => IfProviderAdminAsync(
            providerId,
            async provider =>
            {
                var billingAddress = request.ToDomain();
                var result = await updateBillingAddressCommand.Run(provider, billingAddress);
                return Handle(result);
            });

    [HttpGet("credit")]
    public Task<IResult> GetCreditAsync(
        [FromRoute] Guid providerId)
        => IfProviderAdminAsync(
            providerId,
            async provider =>
            {
                var credit = await getCreditQuery.Run(provider);
                return TypedResults.Ok(credit);
            });

    [HttpPost("credit/bitpay")]
    public Task<IResult> AddCreditViaBitPayAsync(
        [FromRoute] Guid providerId,
        [FromBody] BitPayCreditRequest request)
        => IfProviderAdminAsync(
            providerId,
            async provider =>
            {
                var result = await createBitPayInvoiceForCreditCommand.Run(
                    provider,
                    request.Amount,
                    request.RedirectUrl);
                return Handle(result);
            });

    [HttpGet("payment-method")]
    public Task<IResult> GetPaymentMethodAsync(
        [FromRoute] Guid providerId)
        => IfProviderAdminAsync(
            providerId,
            async provider =>
            {
                var paymentMethod = await getPaymentMethodQuery.Run(provider);
                return TypedResults.Ok(paymentMethod);
            });

    [HttpPut("payment-method")]
    public Task<IResult> UpdatePaymentMethodAsync(
        [FromRoute] Guid providerId,
        [FromBody] TokenizedPaymentMethodRequest request)
        => IfProviderAdminAsync(
            providerId,
            async provider =>
            {
                var (paymentMethod, billingAddress) = request.ToDomain();
                var result = await updatePaymentMethodCommand.Run(provider, paymentMethod, billingAddress);
                return Handle(result);
            });

    [HttpPost("payment-method/verify-bank-account")]
    public Task<IResult> VerifyBankAccountAsync(
        [FromRoute] Guid providerId,
        [FromBody] VerifyBankAccountRequest request)
        => IfProviderAdminAsync(
            providerId,
            async provider =>
            {
                var result = await verifyBankAccountCommand.Run(provider, request.DescriptorCode);
                return Handle(result);
            });

    private Task<IResult> IfProviderAdminAsync(
        Guid providerId,
        Func<Provider, Task<IResult>> function) =>
        RunIfAuthorizedAsync(providerId, currentContext.ProviderProviderAdmin, function);

    private async Task<IResult> RunIfAuthorizedAsync(
        Guid providerId,
        Func<Guid, bool> authorize,
        Func<Provider, Task<IResult>> function)
    {
        var provider = await providerRepository.GetByIdAsync(providerId);

        if (provider is not { Type: ProviderType.BusinessUnit or ProviderType.Msp })
        {
            return Error.NotFound();
        }

        if (!authorize(providerId))
        {
            return Error.Unauthorized();
        }

        return await function(provider);
    }
}
