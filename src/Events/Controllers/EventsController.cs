using System;
using Microsoft.AspNetCore.Mvc;

namespace Events.Controllers
{
    public class EventsController : Controller
    {
        [HttpPost("~/")]
        public void Post([FromBody]string value)
        {
        }
    }
}
