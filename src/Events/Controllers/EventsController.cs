using System;
using Microsoft.AspNetCore.Mvc;

namespace Events.Controllers
{
    public class EventsController : Controller
    {
        [HttpPost]
        [Route("~/")]
        public void Post([FromBody]string value)
        {
        }
    }
}
