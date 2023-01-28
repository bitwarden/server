using System.Diagnostics;
using Bit.Sso.Models;
using IdentityServer4.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Sso.Controllers;

public class HomeController : Controller
{
    private readonly IIdentityServerInteractionService _interaction;

    public HomeController(IIdentityServerInteractionService interaction)
    {
        _interaction = interaction;
    }

    [Route("~/Error")]
    [Route("~/Home/Error")]
    [AllowAnonymous]
    public async Task<IActionResult> Error(string errorId)
    {
        var vm = new ErrorViewModel();

        // retrieve error details from identityserver
        var message = string.IsNullOrWhiteSpace(errorId) ? null :
            await _interaction.GetErrorContextAsync(errorId);
        if (message != null)
        {
            vm.Error = message;
        }
        else
        {
            vm.RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            var exceptionHandlerPathFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            var exception = exceptionHandlerPathFeature?.Error;
            if (exception is InvalidOperationException opEx && opEx.Message.Contains("schemes are: "))
            {
                // Messages coming from aspnetcore with a message
                //  similar to "The registered sign-in schemes are: {schemes}."
                //  will expose other Org IDs and sign-in schemes enabled on
                //  the server. These errors should be truncated to just the
                //  scheme impacted (always the first sentence)
                var cleanupPoint = opEx.Message.IndexOf(". ") + 1;
                var exMessage = opEx.Message.Substring(0, cleanupPoint);
                exception = new InvalidOperationException(exMessage, opEx);
            }
            vm.Exception = exception;
        }

        return View("Error", vm);
    }
}
