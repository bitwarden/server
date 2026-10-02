using Microsoft.AspNetCore.Mvc;

namespace Bit.Notifications.Controllers;

public class InfoController : Controller
{
    [HttpGet("~/alive")]
    [HttpGet("~/now")]
    public DateTime GetAlive()
    {
        return DateTime.UtcNow;
    }
}
