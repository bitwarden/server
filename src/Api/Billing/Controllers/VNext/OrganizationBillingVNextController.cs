using Bit.Api.Billing.Models.Requests.Payment;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Payment.Commands;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Context;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers.VNext;

[Route("organizations/{organizationId:guid}/billing/vnext")]
[SelfHosted(NotSelfHostedOnly = true)]
public class OrganizationBillingVNextController(
    ICurrentContext currentContext,
    ICreateBitPayInvoiceForCreditCommand createBitPayInvoiceForCreditCommand,
    IGetBillingAddressQuery getBillingAddressQuery,
    IGetCreditQuery getCreditQuery,
    IGetPaymentMethodQuery getPaymentMethodQuery,
    IOrganizationRepository organizationRepository,
    IUpdateBillingAddressCommand updateBillingAddressCommand,
    IUpdatePaymentMethodCommand updatePaymentMethodCommand,
    IVerifyBankAccountCommand verifyBankAccountCommand) : BaseBillingController
{
    [HttpGet("address")]
    public Task<IResult> GetBillingAddressAsync(
        [FromRoute] Guid organizationId)
        => IfCanEditPaymentMethodAsync(
            organizationId,
            async organization =>
            {
                var billingAddress = await getBillingAddressQuery.Run(organization);
                return TypedResults.Ok(billingAddress);
            });

    [HttpPut("address")]
    public Task<IResult> UpdateBillingAddressAsync(
        [FromRoute] Guid organizationId,
        [FromBody] BillingAddressRequest request)
        => IfCanEditPaymentMethodAsync(
            organizationId,
            async organization =>
            {
                var billingAddress = request.ToDomain();
                var result = await updateBillingAddressCommand.Run(organization, billingAddress);
                return Handle(result);
            });

    [HttpGet("credit")]
    public Task<IResult> GetCreditAsync(
        [FromRoute] Guid organizationId)
        => IfCanEditPaymentMethodAsync(
            organizationId,
            async organization =>
            {
                var credit = await getCreditQuery.Run(organization);
                return TypedResults.Ok(credit);
            });

    [HttpPost("credit/bitpay")]
    public Task<IResult> AddCreditViaBitPayAsync(
        [FromRoute] Guid organizationId,
        [FromBody] BitPayCreditRequest request)
        => IfCanEditPaymentMethodAsync(
            organizationId,
            async organization =>
            {
                var result = await createBitPayInvoiceForCreditCommand.Run(
                    organization,
                    request.Amount,
                    request.RedirectUrl);
                return Handle(result);
            });

    [HttpGet("payment-method")]
    public Task<IResult> GetPaymentMethodAsync(
        [FromRoute] Guid organizationId)
        => IfCanEditPaymentMethodAsync(
            organizationId,
            async organization =>
            {
                var paymentMethod = await getPaymentMethodQuery.Run(organization);
                return TypedResults.Ok(paymentMethod);
            });

    [HttpPut("payment-method")]
    public Task<IResult> UpdatePaymentMethodAsync(
        [FromRoute] Guid organizationId,
        [FromBody] TokenizedPaymentMethodRequest request)
        => IfCanEditPaymentMethodAsync(
            organizationId,
            async organization =>
            {
                var (paymentMethod, billingAddress) = request.ToDomain();
                var result = await updatePaymentMethodCommand.Run(organization, paymentMethod, billingAddress);
                return Handle(result);
            });

    [HttpPost("payment-method/verify-bank-account")]
    public Task<IResult> VerifyBankAccountAsync(
        [FromRoute] Guid organizationId,
        [FromBody] VerifyBankAccountRequest request)
        => IfCanEditPaymentMethodAsync(
            organizationId,
            async organization =>
            {
                var result = await verifyBankAccountCommand.Run(organization, request.DescriptorCode);
                return Handle(result);
            });

    private Task<IResult> IfCanEditPaymentMethodAsync(
        Guid organizationId,
        Func<Organization, Task<IResult>> function) => RunIfAuthorizedAsync(organizationId, currentContext.EditPaymentMethods, function);

    private async Task<IResult> RunIfAuthorizedAsync(
        Guid organizationId,
        Func<Guid, Task<bool>> authorize,
        Func<Organization, Task<IResult>> function)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return Error.NotFound();
        }

        if (!await authorize(organizationId))
        {
            return Error.Unauthorized();
        }

        return await function(organization);
    }
}
