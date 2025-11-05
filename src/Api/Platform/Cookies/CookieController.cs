using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Api.Platform.Cookies;

[Route("cookies")]
[SelfHosted(SelfHostedOnly = true)]
public class CookieController : ControllerBase
{
    [HttpGet("test")]
    public IActionResult GetTestCookie()
    {
        var cookieValue = Request.Cookies["BWSessionCookie"];

        if (string.IsNullOrEmpty(cookieValue))
        {
            return NotFound(new { message = "Cookie not found in request" });
        }

        return Ok(new { cookieValue });
    }
}
