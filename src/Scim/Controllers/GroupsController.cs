using System;
using System.Threading.Tasks;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Bit.Scim.Models;
using System.Diagnostics;
using System.IO;
using Bit.Core.Services;
using Bit.Core.Exceptions;

namespace Bit.Scim.Controllers
{
    [Route("groups")]
    [Route("scim/groups")]
    public class GroupsController : BaseController
    {
        private readonly IGroupRepository _groupRepository;
        private readonly IGroupService _groupService;
        private Guid _orgId = new Guid("2933f760-9c0b-4efb-a437-a82a00ed3fc1"); // TODO: come from context

        public GroupsController(
            IGroupRepository groupRepository,
            IGroupService groupService)
        {
            _groupRepository = groupRepository;
            _groupService = groupService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery]string filter, [FromQuery]string excludedAttributes,
            [FromQuery]string attributes)
        {
            var groups = await _groupRepository.GetManyByOrganizationIdAsync(_orgId);
            groups = FilterResources(groups, filter);
            var groupsResult = groups.Select(g => new ScimGroup(g));
            var result = new ScimListResponse(groupsResult);
            return new OkObjectResult(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string id)
        {
            var group = await _groupRepository.GetByIdAsync(new Guid(id));
            if(group == null || group.OrganizationId != _orgId)
            {
                throw new NotFoundException();
            }

            var result = new ScimGroup(group);
            return new OkObjectResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody]ScimGroup model)
        {
            var group = model.ToGroup(_orgId);
            await _groupService.SaveAsync(group);
            var result = new ScimGroup(group);
            var getUrl = Url.Action("Get", "Groups", new { id = group.Id.ToString() }, Request.Protocol, Request.Host.Value);
            return new CreatedResult(getUrl, result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(string id, [FromBody]ScimGroup model)
        {
            var group = await _groupRepository.GetByIdAsync(new Guid(id));
            if(group == null || group.OrganizationId != _orgId)
            {
                throw new NotFoundException();
            }

            group = model.ToGroup(group);
            await _groupService.SaveAsync(group);

            var result = new ScimGroup(group);
            return new OkObjectResult(result);
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> Patch(string id)
        {
            var group = await _groupRepository.GetByIdAsync(new Guid(id));
            if(group == null || group.OrganizationId != _orgId)
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

            // TODO: Do patch
            
            var result = new ScimGroup(group);
            return new OkObjectResult(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var group = await _groupRepository.GetByIdAsync(new Guid(id));
            if(group == null || group.OrganizationId != _orgId)
            {
                throw new NotFoundException();
            }

            await _groupService.DeleteAsync(group);
            return new OkResult();
        }
    }
}
