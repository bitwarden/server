using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Public.Controllers
{
    [Route("public/groups")]
    [Authorize("Organization")]
    public class GroupsController : Controller
    {
        [HttpGet("{id}")]
        public JsonResult Get(string id)
        {
            return new JsonResult("Hello " + id);
        }
    }
}
