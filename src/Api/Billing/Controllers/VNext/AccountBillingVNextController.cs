using Bit.Api.Billing.Models.Requests.Payment;
using Bit.Core.Billing.Payment.Commands;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers.VNext;

[Route("account/billing/vnext")]
[SelfHosted(NotSelfHostedOnly = true)]
public class AccountBillingVNextController(
    ICreateBitPayInvoiceForCreditCommand createBitPayInvoiceForCreditCommand,
    IGetCreditQuery getCreditQuery,
    IGetPaymentMethodQuery getPaymentMethodQuery,
    IUpdatePaymentMethodCommand updatePaymentMethodCommand,
    IUserService userService) : BaseBillingController
{
    [HttpGet("credit")]
    public Task<IResult> GetCreditAsync()
        => IfAuthorizedAsync(
            async user =>
            {
                var credit = await getCreditQuery.Run(user);
                return TypedResults.Ok(credit);
            });

    [HttpPost("credit/bitpay")]
    public Task<IResult> AddCreditViaBitPayAsync(
        [FromBody] BitPayCreditRequest request)
        => IfAuthorizedAsync(
            async user =>
            {
                var result = await createBitPayInvoiceForCreditCommand.Run(
                    user,
                    request.Amount,
                    request.RedirectUrl);
                return Handle(result);
            });

    [HttpGet("payment-method")]
    public Task<IResult> GetPaymentMethodAsync()
        => IfAuthorizedAsync(
            async user =>
            {
                var paymentMethod = await getPaymentMethodQuery.Run(user);
                return TypedResults.Ok(paymentMethod);
            });

    [HttpPut("payment-method")]
    public Task<IResult> UpdatePaymentMethodAsync(
        [FromBody] TokenizedPaymentMethodRequest request)
        => IfAuthorizedAsync(
            async user =>
            {
                var (paymentMethod, billingAddress) = request.ToDomain();
                var result = await updatePaymentMethodCommand.Run(user, paymentMethod, billingAddress);
                return Handle(result);
            });

    private async Task<IResult> IfAuthorizedAsync(
        Func<User, Task<IResult>> function)
    {
        var user = await userService.GetUserByPrincipalAsync(User);

        if (user == null)
        {
            return Error.Unauthorized();
        }

        return await function(user);
    }
}
