using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Scim.Controllers;

[AllowAnonymous]
public class InfoController : Controller
{
    [HttpGet("~/alive")]
    [HttpGet("~/now")]
    public DateTime GetAlive()
    {
        return DateTime.UtcNow;
    }

    [HttpGet("~/version")]
    public JsonResult GetVersion()
    {
        return Json(AssemblyHelpers.GetVersion());
    }
}
