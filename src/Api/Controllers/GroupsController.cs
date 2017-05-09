using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Bit.Core.Models.Api;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core;

namespace Bit.Api.Controllers
{
    [Route("organizations/{orgId}/groups")]
    [Authorize("Application")]
    public class GroupsController : Controller
    {
        private readonly IGroupRepository _groupRepository;
        private readonly IUserService _userService;
        private readonly CurrentContext _currentContext;

        public GroupsController(
            IGroupRepository groupRepository,
            IUserService userService,
            CurrentContext currentContext)
        {
            _groupRepository = groupRepository;
            _userService = userService;
            _currentContext = currentContext;
        }

        [HttpGet("{id}")]
        public async Task<GroupResponseModel> Get(string orgId, string id)
        {
            var group = await _groupRepository.GetByIdAsync(new Guid(id));
            if(group == null || !_currentContext.OrganizationAdmin(group.OrganizationId))
            {
                throw new NotFoundException();
            }

            return new GroupResponseModel(group);
        }

        [HttpGet("{id}/details")]
        public async Task<GroupDetailsResponseModel> GetDetails(string orgId, string id)
        {
            var groupDetails = await _groupRepository.GetByIdWithCollectionsAsync(new Guid(id));
            if(groupDetails?.Item1 == null || !_currentContext.OrganizationAdmin(groupDetails.Item1.OrganizationId))
            {
                throw new NotFoundException();
            }

            return new GroupDetailsResponseModel(groupDetails.Item1, groupDetails.Item2);
        }

        [HttpGet("")]
        public async Task<ListResponseModel<GroupResponseModel>> Get(string orgId)
        {
            var orgIdGuid = new Guid(orgId);
            if(!_currentContext.OrganizationAdmin(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var groups = await _groupRepository.GetManyByOrganizationIdAsync(orgIdGuid);
            var responses = groups.Select(g => new GroupResponseModel(g));
            return new ListResponseModel<GroupResponseModel>(responses);
        }

        [HttpPost("")]
        public async Task<GroupResponseModel> Post(string orgId, [FromBody]GroupRequestModel model)
        {
            var orgIdGuid = new Guid(orgId);
            if(!_currentContext.OrganizationAdmin(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var group = model.ToGroup(orgIdGuid);
            if(model.CollectionIds == null)
            {
                await _groupRepository.CreateAsync(group);
            }
            else
            {
                await _groupRepository.CreateAsync(group, model.CollectionIds.Select(c => new Guid(c)));
            }

            return new GroupResponseModel(group);
        }

        [HttpPut("{id}")]
        [HttpPost("{id}")]
        public async Task<GroupResponseModel> Put(string orgId, string id, [FromBody]GroupRequestModel model)
        {
            var group = await _groupRepository.GetByIdAsync(new Guid(id));
            if(group == null || !_currentContext.OrganizationAdmin(group.OrganizationId))
            {
                throw new NotFoundException();
            }

            if(model.CollectionIds == null)
            {
                await _groupRepository.ReplaceAsync(model.ToGroup(group));
            }
            else
            {
                await _groupRepository.ReplaceAsync(model.ToGroup(group), model.CollectionIds.Select(c => new Guid(c)));
            }

            return new GroupResponseModel(group);
        }

        [HttpDelete("{id}")]
        [HttpPost("{id}/delete")]
        public async Task Delete(string orgId, string id)
        {
            var group = await _groupRepository.GetByIdAsync(new Guid(id));
            if(group == null || !_currentContext.OrganizationAdmin(group.OrganizationId))
            {
                throw new NotFoundException();
            }

            await _groupRepository.DeleteAsync(group);
        }
    }
}
