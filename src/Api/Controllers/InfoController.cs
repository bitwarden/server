using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

public class InfoController : Controller
{
    [HttpGet("~/alive")]
    public DateTime GetAlive()
    {
        return DateTime.UtcNow;
    }

    [HttpGet("~/now")]
    [Obsolete("This endpoint is deprecated. Use GET /alive instead.")]
    public DateTime GetNow()
    {
        return GetAlive();
    }

    [HttpGet("~/version")]
    public JsonResult GetVersion()
    {
        return Json(AssemblyHelpers.GetVersion());
    }
}
