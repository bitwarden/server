using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Models;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Models.Data;
using Bit.Core.Exceptions;

namespace Bit.Scim.Controllers
{
    [Route("users")]
    [Route("scim/users")]
    public class UsersController : BaseController
    {
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IOrganizationService _organizationService;
        private Guid _orgId = new Guid("2933f760-9c0b-4efb-a437-a82a00ed3fc1"); // TODO: come from context

        public UsersController(
            IOrganizationUserRepository organizationUserRepository,
            IOrganizationService organizationService)
        {
            _organizationUserRepository = organizationUserRepository;
            _organizationService = organizationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery]string filter, [FromQuery]string excludedAttributes,
            [FromQuery]string attributes)
        {
            var users = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(_orgId);
            users = FilterResources(users, filter);
            var usersResult = users.Select(u => new ScimUser(u));
            var result = new ScimListResponse(usersResult);
            return new OkObjectResult(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string id)
        {
            var users = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(_orgId);
            var user = users.SingleOrDefault(u => u.Id == new Guid(id));
            if(user == null)
            {
                throw new NotFoundException();
            }

            var result = new ScimUser(user);
            return new OkObjectResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody]ScimUser model)
        {
            var email = model.Emails?.FirstOrDefault();
            if(email == null)
            {
                throw new BadRequestException("No email address available.");
            }

            var orgUser = await _organizationService.InviteUserAsync(_orgId, null, email.Value,
                OrganizationUserType.User, false, model.ExternalId, new List<SelectionReadOnly>());
            var result = new ScimUser(orgUser);
            var getUrl = Url.Action("Get", "Users", new { id = orgUser.Id.ToString() }, Request.Protocol, Request.Host.Value);
            return new CreatedResult(getUrl, result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(string id, [FromBody]ScimUser model)
        {
            var users = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(_orgId);
            var user = users.SingleOrDefault(u => u.Id == new Guid(id));
            if(user == null)
            {
                throw new NotFoundException();
            }

            // TODO: update

            var result = new ScimUser(user);
            return new OkObjectResult(result);
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> Patch(string id)
        {
            var users = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(_orgId);
            var user = users.SingleOrDefault(u => u.Id == new Guid(id));
            if(user == null)
            {
                throw new NotFoundException();
            }

            var memstream = new MemoryStream();
            Request.Body.CopyTo(memstream);
            memstream.Position = 0;
            using(var reader = new StreamReader(memstream))
            {
                var text = reader.ReadToEnd();
                Debug.WriteLine(text);
            }

            // TODO: patch

            var result = new ScimUser(user);
            return new OkObjectResult(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            await _organizationService.DeleteUserAsync(_orgId, new Guid(id), null);
            return new OkResult();
        }
    }
}
