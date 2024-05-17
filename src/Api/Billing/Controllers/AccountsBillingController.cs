using Bit.Api.Models.Response;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

[Route("accounts/billing")]
[Authorize("Application")]
public class AccountsBillingController(
    IPaymentService paymentService,
    IUserService userService) : Controller
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

        var billingInfo = await paymentService.GetBillingBalanceAndSourceAsync(user);
        return new BillingPaymentResponseModel(billingInfo);
    }
}
