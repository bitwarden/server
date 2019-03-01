using System;
using Bit.Core.Models.Api.Public;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Public.Controllers
{
    [Route("public/groups")]
    [Authorize("Organization")]
    public class GroupsController : Controller
    {
        /// <summary>
        /// Retrieves a specific product by unique id
        /// </summary>
        /// <remarks>Awesomeness!</remarks>
        /// <response code="200">Group created</response>
        /// <response code="400">Group has missing/invalid values</response>
        /// <response code="500">Oops! Can't create your product right now</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(GroupResponseModel), 200)]
        public IActionResult Get(Guid id)
        {
            return new JsonResult(new GroupResponseModel(new Core.Models.Table.Group
            {
                Id = id,
                Name = "test",
                OrganizationId = Guid.NewGuid()
            }));
        }
    }
}
