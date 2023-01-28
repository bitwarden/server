using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Admin.Controllers;

public class ErrorController : Controller
{
    [Route("/error")]
    public IActionResult Error(int? statusCode = null)
    {
        var exceptionHandlerPathFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        TempData["Error"] = HttpContext.Features.Get<IExceptionHandlerFeature>()?.Error.Message;

        if (exceptionHandlerPathFeature != null)
        {
            return Redirect(exceptionHandlerPathFeature.Path);
        }
        else
        {
            return Redirect("/Home");
        }
    }
}
