#nullable enable
using Bit.Api.Billing.Models.Responses;
using Bit.Core.Billing.Services;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

[Route("accounts/billing")]
[Authorize("Application")]
public class AccountsBillingController(
    IPaymentService paymentService,
    IUserService userService,
    IPaymentHistoryService paymentHistoryService) : Controller
{
    [HttpGet("history")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<BillingHistoryResponseModel> GetBillingHistoryAsync()
    {
        var user = await userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var billingInfo = await paymentService.GetBillingHistoryAsync(user);
        return new BillingHistoryResponseModel(billingInfo);
    }

    [HttpGet("payment-method")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<BillingPaymentResponseModel> GetPaymentMethodAsync()
    {
        var user = await userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var billingInfo = await paymentService.GetBillingAsync(user);
        return new BillingPaymentResponseModel(billingInfo);
    }

    [HttpGet("invoices")]
    public async Task<IResult> GetInvoicesAsync([FromQuery] string? status = null, [FromQuery] string? startAfter = null)
    {
        var user = await userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var invoices = await paymentHistoryService.GetInvoiceHistoryAsync(
            user,
            5,
            status,
            startAfter);

        return TypedResults.Ok(invoices);
    }

    [HttpGet("transactions")]
    public async Task<IResult> GetTransactionsAsync([FromQuery] DateTime? startAfter = null)
    {
        var user = await userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var transactions = await paymentHistoryService.GetTransactionHistoryAsync(
            user,
            5,
            startAfter);

        return TypedResults.Ok(transactions);
    }
}
