using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Identity.Controllers;

public class InfoController : Controller
{
    [HttpGet("~/alive")]
    [HttpGet("~/now")]
    public DateTime GetAlive()
    {
        return DateTime.UtcNow;
    }

    [HttpGet("~/hello")]
    public IActionResult Hello()
    {
        return Content("Hello, this is Identity!", "text/plain");
    }

    [HttpGet("~/version")]
    public JsonResult GetVersion()
    {
        return Json(AssemblyHelpers.GetVersion());
    }
}
