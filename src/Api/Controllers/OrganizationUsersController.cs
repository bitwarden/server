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
using System.Collections.Generic;

namespace Bit.Api.Controllers
{
    [Route("organizations/{orgId}/users")]
    [Authorize("Application")]
    public class OrganizationUsersController : Controller
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IOrganizationService _organizationService;
        private readonly ICollectionRepository _collectionRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IUserService _userService;
        private readonly CurrentContext _currentContext;

        public OrganizationUsersController(
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IOrganizationService organizationService,
            ICollectionRepository collectionRepository,
            IGroupRepository groupRepository,
            IUserService userService,
            CurrentContext currentContext)
        {
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _organizationService = organizationService;
            _collectionRepository = collectionRepository;
            _groupRepository = groupRepository;
            _userService = userService;
            _currentContext = currentContext;
        }

        [HttpGet("{id}")]
        public async Task<OrganizationUserDetailsResponseModel> Get(string orgId, string id)
        {
            var organizationUser = await _organizationUserRepository.GetByIdWithCollectionsAsync(new Guid(id));
            if(organizationUser == null || !_currentContext.OrganizationAdmin(organizationUser.Item1.OrganizationId))
            {
                throw new NotFoundException();
            }

            return new OrganizationUserDetailsResponseModel(organizationUser.Item1, organizationUser.Item2);
        }

        [HttpGet("")]
        public async Task<ListResponseModel<OrganizationUserUserDetailsResponseModel>> Get(string orgId)
        {
            var orgGuidId = new Guid(orgId);
            if(!_currentContext.OrganizationManager(orgGuidId))
            {
                throw new NotFoundException();
            }

            var organizationUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(orgGuidId);
            var responseTasks = organizationUsers.Select(async o => new OrganizationUserUserDetailsResponseModel(o,
                await _userService.TwoFactorIsEnabledAsync(o)));
            var responses = await Task.WhenAll(responseTasks);
            return new ListResponseModel<OrganizationUserUserDetailsResponseModel>(responses);
        }

        [HttpGet("{id}/groups")]
        public async Task<IEnumerable<string>> GetGroups(string orgId, string id)
        {
            var organizationUser = await _organizationUserRepository.GetByIdAsync(new Guid(id));
            if(organizationUser == null || !_currentContext.OrganizationAdmin(organizationUser.OrganizationId))
            {
                throw new NotFoundException();
            }

            var groupIds = await _groupRepository.GetManyIdsByUserIdAsync(organizationUser.Id);
            var responses = groupIds.Select(g => g.ToString());
            return responses;
        }

        [HttpPost("invite")]
        public async Task Invite(string orgId, [FromBody]OrganizationUserInviteRequestModel model)
        {
            var orgGuidId = new Guid(orgId);
            if(!_currentContext.OrganizationAdmin(orgGuidId))
            {
                throw new NotFoundException();
            }

            var userId = _userService.GetProperUserId(User);
            var result = await _organizationService.InviteUserAsync(orgGuidId, userId.Value, model.Emails, model.Type.Value,
                model.AccessAll, null, model.Collections?.Select(c => c.ToSelectionReadOnly()));
        }

        [HttpPost("{id}/reinvite")]
        public async Task Reinvite(string orgId, string id)
        {
            var orgGuidId = new Guid(orgId);
            if(!_currentContext.OrganizationAdmin(orgGuidId))
            {
                throw new NotFoundException();
            }

            var userId = _userService.GetProperUserId(User);
            await _organizationService.ResendInviteAsync(orgGuidId, userId.Value, new Guid(id));
        }

        [HttpPost("{id}/accept")]
        public async Task Accept(string orgId, string id, [FromBody]OrganizationUserAcceptRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if(user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var result = await _organizationService.AcceptUserAsync(new Guid(id), user, model.Token);
        }

        [HttpPost("{id}/confirm")]
        public async Task Confirm(string orgId, string id, [FromBody]OrganizationUserConfirmRequestModel model)
        {
            var orgGuidId = new Guid(orgId);
            if(!_currentContext.OrganizationAdmin(orgGuidId))
            {
                throw new NotFoundException();
            }

            var userId = _userService.GetProperUserId(User);
            var result = await _organizationService.ConfirmUserAsync(orgGuidId, new Guid(id), model.Key, userId.Value);
        }

        [HttpPut("{id}")]
        [HttpPost("{id}")]
        public async Task Put(string orgId, string id, [FromBody]OrganizationUserUpdateRequestModel model)
        {
            var orgGuidId = new Guid(orgId);
            if(!_currentContext.OrganizationAdmin(orgGuidId))
            {
                throw new NotFoundException();
            }

            var organizationUser = await _organizationUserRepository.GetByIdAsync(new Guid(id));
            if(organizationUser == null || organizationUser.OrganizationId != orgGuidId)
            {
                throw new NotFoundException();
            }

            var userId = _userService.GetProperUserId(User);
            await _organizationService.SaveUserAsync(model.ToOrganizationUser(organizationUser), userId.Value,
                model.Collections?.Select(c => c.ToSelectionReadOnly()));
        }

        [HttpPut("{id}/groups")]
        [HttpPost("{id}/groups")]
        public async Task PutGroups(string orgId, string id, [FromBody]OrganizationUserUpdateGroupsRequestModel model)
        {
            var orgGuidId = new Guid(orgId);
            if(!_currentContext.OrganizationAdmin(orgGuidId))
            {
                throw new NotFoundException();
            }

            var organizationUser = await _organizationUserRepository.GetByIdAsync(new Guid(id));
            if(organizationUser == null || organizationUser.OrganizationId != orgGuidId)
            {
                throw new NotFoundException();
            }

            if(organizationUser.Type == Core.Enums.OrganizationUserType.Owner && !_currentContext.OrganizationOwner(orgGuidId))
            {
                throw new BadRequestException("Only owners can update other owners.");
            }

            await _organizationService.UpdateUserGroupsAsync(organizationUser, model.GroupIds.Select(g => new Guid(g)));
        }

        [HttpDelete("{id}")]
        [HttpPost("{id}/delete")]
        public async Task Delete(string orgId, string id)
        {
            var orgGuidId = new Guid(orgId);
            if(!_currentContext.OrganizationAdmin(orgGuidId))
            {
                throw new NotFoundException();
            }

            var userId = _userService.GetProperUserId(User);
            await _organizationService.DeleteUserAsync(orgGuidId, new Guid(id), userId.Value);
        }
    }
}
