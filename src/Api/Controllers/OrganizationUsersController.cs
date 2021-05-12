using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Bit.Core.Models.Api;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Context;
using System.Collections.Generic;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;

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
        private readonly ICurrentContext _currentContext;

        public OrganizationUsersController(
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IOrganizationService organizationService,
            ICollectionRepository collectionRepository,
            IGroupRepository groupRepository,
            IUserService userService,
            ICurrentContext currentContext)
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
            if (organizationUser == null || !_currentContext.ManageUsers(organizationUser.Item1.OrganizationId))
            {
                throw new NotFoundException();
            }

            return new OrganizationUserDetailsResponseModel(organizationUser.Item1, organizationUser.Item2);
        }

        [HttpGet("")]
        public async Task<ListResponseModel<OrganizationUserUserDetailsResponseModel>> Get(string orgId)
        {
            var orgGuidId = new Guid(orgId);
            if (!_currentContext.ManageAssignedCollections(orgGuidId) &&
                !_currentContext.ManageGroups(orgGuidId) &&
                !_currentContext.ManageUsers(orgGuidId))
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
            if (organizationUser == null || (!_currentContext.ManageGroups(organizationUser.OrganizationId) &&
                                             !_currentContext.ManageUsers(organizationUser.OrganizationId)))
            {
                throw new NotFoundException();
            }

            var groupIds = await _groupRepository.GetManyIdsByUserIdAsync(organizationUser.Id);
            var responses = groupIds.Select(g => g.ToString());
            return responses;
        }
        
        [HttpGet("{id}/reset-password-details")]
        public async Task<OrganizationUserResetPasswordDetailsResponseModel> GetResetPasswordDetails(string orgId, string id)
        {
            // Make sure the calling user can reset passwords for this org
            var orgGuidId = new Guid(orgId);
            if (!_currentContext.ManageResetPassword(orgGuidId))
            {
                throw new NotFoundException();
            }

            var organizationUser = await _organizationUserRepository.GetByIdAsync(new Guid(id));
            if (organizationUser == null || !organizationUser.UserId.HasValue)
            {
                throw new NotFoundException();
            }

            // Retrieve data necessary for response (KDF, KDF Iterations, ResetPasswordKey)
            // TODO Revisit this and create SPROC to reduce DB calls
            var user = await _userService.GetUserByIdAsync(organizationUser.UserId.Value);
            if (user == null)
            {
                throw new NotFoundException();
            }

            return new OrganizationUserResetPasswordDetailsResponseModel(new OrganizationUserResetPasswordDetails(organizationUser, user));
        }

        [HttpPost("invite")]
        public async Task Invite(string orgId, [FromBody]OrganizationUserInviteRequestModel model)
        {
            var orgGuidId = new Guid(orgId);
            if (!_currentContext.ManageUsers(orgGuidId))
            {
                throw new NotFoundException();
            }

            var userId = _userService.GetProperUserId(User);
            var result = await _organizationService.InviteUserAsync(orgGuidId, userId.Value, null, new OrganizationUserInvite(model));
        }
        
        [HttpPost("reinvite")]
        public async Task BulkReinvite(string orgId, [FromBody]OrganizationUserBulkReinviteRequestModel model)
        {
            var orgGuidId = new Guid(orgId);
            if (!_currentContext.ManageUsers(orgGuidId))
            {
                throw new NotFoundException();
            }

            var userId = _userService.GetProperUserId(User);
            await _organizationService.ResendInvitesAsync(orgGuidId, userId.Value, model.Ids);
        }

        [HttpPost("{id}/reinvite")]
        public async Task Reinvite(string orgId, string id)
        {
            var orgGuidId = new Guid(orgId);
            if (!_currentContext.ManageUsers(orgGuidId))
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
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var result = await _organizationService.AcceptUserAsync(new Guid(id), user, model.Token, _userService);
        }

        [HttpPost("{id}/confirm")]
        public async Task Confirm(string orgId, string id, [FromBody]OrganizationUserConfirmRequestModel model)
        {
            var orgGuidId = new Guid(orgId);
            if (!_currentContext.ManageUsers(orgGuidId))
            {
                throw new NotFoundException();
            }

            var userId = _userService.GetProperUserId(User);
            var result = await _organizationService.ConfirmUserAsync(orgGuidId, new Guid(id), model.Key, userId.Value,
                _userService);
        }

        [HttpPut("{id}")]
        [HttpPost("{id}")]
        public async Task Put(string orgId, string id, [FromBody]OrganizationUserUpdateRequestModel model)
        {
            var orgGuidId = new Guid(orgId);
            if (!_currentContext.ManageUsers(orgGuidId))
            {
                throw new NotFoundException();
            }

            var organizationUser = await _organizationUserRepository.GetByIdAsync(new Guid(id));
            if (organizationUser == null || organizationUser.OrganizationId != orgGuidId)
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
            if (!_currentContext.ManageUsers(orgGuidId))
            {
                throw new NotFoundException();
            }

            var organizationUser = await _organizationUserRepository.GetByIdAsync(new Guid(id));
            if (organizationUser == null || organizationUser.OrganizationId != orgGuidId)
            {
                throw new NotFoundException();
            }

            var loggedInUserId = _userService.GetProperUserId(User);
            await _organizationService.UpdateUserGroupsAsync(organizationUser, model.GroupIds.Select(g => new Guid(g)), loggedInUserId);
        }
        
        [HttpPut("{userId}/reset-password-enrollment")]
        public async Task PutResetPasswordEnrollment(string orgId, string userId, [FromBody]OrganizationUserResetPasswordEnrollmentRequestModel model)
        {
            var callingUserId = _userService.GetProperUserId(User);
            await _organizationService.UpdateUserResetPasswordEnrollmentAsync(new Guid(orgId), new Guid(userId), model.ResetPasswordKey, callingUserId);
        }
        
        [HttpPut("{id}/reset-password")]
        public async Task PutResetPassword(string orgId, string id, [FromBody]OrganizationUserResetPasswordRequestModel model)
        {
            var orgGuidId = new Guid(orgId);
            // Calling user must have Manage Reset Password permission
            if (!_currentContext.ManageResetPassword(orgGuidId))
            {
                throw new NotFoundException();
            }
            
            var orgUser = await _organizationUserRepository.GetByIdAsync(new Guid(id));
            if (orgUser == null || orgUser.Status != OrganizationUserStatusType.Confirmed ||
                orgUser.OrganizationId != orgGuidId || string.IsNullOrEmpty(orgUser.ResetPasswordKey) ||
                !orgUser.UserId.HasValue)
            {
                throw new BadRequestException("Organization User not valid");
            }

            var user = await _userService.GetUserByIdAsync(orgUser.UserId.Value);
            if (user == null)
            {
                throw new NotFoundException();
            }


            var result = await _userService.AdminResetPasswordAsync(user, model.NewMasterPasswordHash, model.Key);
            if (result.Succeeded)
            {
                return;
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await Task.Delay(2000);
            throw new BadRequestException(ModelState);
            }

        [HttpDelete("{id}")]
        [HttpPost("{id}/delete")]
        public async Task Delete(string orgId, string id)
        {
            var orgGuidId = new Guid(orgId);
            if (!_currentContext.ManageUsers(orgGuidId))
            {
                throw new NotFoundException();
            }

            var userId = _userService.GetProperUserId(User);
            await _organizationService.DeleteUserAsync(orgGuidId, new Guid(id), userId.Value);
        }
    }
}
