using Bit.Api.Models.Request.Organizations;
using Bit.Api.Models.Response;
using Bit.Api.Models.Response.Organizations;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Models.Data.Organizations.Policies;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

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
    private readonly IPolicyRepository _policyRepository;
    private readonly ICurrentContext _currentContext;

    public OrganizationUsersController(
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationService organizationService,
        ICollectionRepository collectionRepository,
        IGroupRepository groupRepository,
        IUserService userService,
        IPolicyRepository policyRepository,
        ICurrentContext currentContext)
    {
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _organizationService = organizationService;
        _collectionRepository = collectionRepository;
        _groupRepository = groupRepository;
        _userService = userService;
        _policyRepository = policyRepository;
        _currentContext = currentContext;
    }

    [HttpGet("{id}")]
    public async Task<OrganizationUserDetailsResponseModel> Get(string orgId, string id)
    {
        var organizationUser = await _organizationUserRepository.GetByIdWithCollectionsAsync(new Guid(id));
        if (organizationUser == null || !await _currentContext.ManageUsers(organizationUser.Item1.OrganizationId))
        {
            throw new NotFoundException();
        }

        return new OrganizationUserDetailsResponseModel(organizationUser.Item1, organizationUser.Item2);
    }

    [HttpGet("")]
    public async Task<ListResponseModel<OrganizationUserUserDetailsResponseModel>> Get(string orgId)
    {
        var orgGuidId = new Guid(orgId);
        if (!await _currentContext.ViewAllCollections(orgGuidId) &&
            !await _currentContext.ViewAssignedCollections(orgGuidId) &&
            !await _currentContext.ManageGroups(orgGuidId) &&
            !await _currentContext.ManageUsers(orgGuidId))
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
        if (organizationUser == null || (!await _currentContext.ManageGroups(organizationUser.OrganizationId) &&
                                         !await _currentContext.ManageUsers(organizationUser.OrganizationId)))
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
        if (!await _currentContext.ManageResetPassword(orgGuidId))
        {
            throw new NotFoundException();
        }

        var organizationUser = await _organizationUserRepository.GetByIdAsync(new Guid(id));
        if (organizationUser == null || !organizationUser.UserId.HasValue)
        {
            throw new NotFoundException();
        }

        // Retrieve data necessary for response (KDF, KDF Iterations, ResetPasswordKey)
        // TODO Reset Password - Revisit this and create SPROC to reduce DB calls
        var user = await _userService.GetUserByIdAsync(organizationUser.UserId.Value);
        if (user == null)
        {
            throw new NotFoundException();
        }

        // Retrieve Encrypted Private Key from organization
        var org = await _organizationRepository.GetByIdAsync(orgGuidId);
        if (org == null)
        {
            throw new NotFoundException();
        }

        return new OrganizationUserResetPasswordDetailsResponseModel(new OrganizationUserResetPasswordDetails(organizationUser, user, org));
    }

    [HttpPost("invite")]
    public async Task Invite(string orgId, [FromBody] OrganizationUserInviteRequestModel model)
    {
        var orgGuidId = new Guid(orgId);
        if (!await _currentContext.ManageUsers(orgGuidId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User);
        var result = await _organizationService.InviteUsersAsync(orgGuidId, userId.Value,
            new (OrganizationUserInvite, string)[] { (new OrganizationUserInvite(model.ToData()), null) });
    }

    [HttpPost("reinvite")]
    public async Task<ListResponseModel<OrganizationUserBulkResponseModel>> BulkReinvite(string orgId, [FromBody] OrganizationUserBulkRequestModel model)
    {
        var orgGuidId = new Guid(orgId);
        if (!await _currentContext.ManageUsers(orgGuidId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User);
        var result = await _organizationService.ResendInvitesAsync(orgGuidId, userId.Value, model.Ids);
        return new ListResponseModel<OrganizationUserBulkResponseModel>(
            result.Select(t => new OrganizationUserBulkResponseModel(t.Item1.Id, t.Item2)));
    }

    [HttpPost("{id}/reinvite")]
    public async Task Reinvite(string orgId, string id)
    {
        var orgGuidId = new Guid(orgId);
        if (!await _currentContext.ManageUsers(orgGuidId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User);
        await _organizationService.ResendInviteAsync(orgGuidId, userId.Value, new Guid(id));
    }

    [HttpPost("{organizationUserId}/accept")]
    public async Task Accept(Guid orgId, Guid organizationUserId, [FromBody] OrganizationUserAcceptRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var masterPasswordPolicy = await _policyRepository.GetByOrganizationIdTypeAsync(orgId, PolicyType.ResetPassword);
        var useMasterPasswordPolicy = masterPasswordPolicy != null &&
            masterPasswordPolicy.Enabled &&
            masterPasswordPolicy.GetDataModel<ResetPasswordDataModel>().AutoEnrollEnabled;

        if (useMasterPasswordPolicy &&
            string.IsNullOrWhiteSpace(model.ResetPasswordKey))
        {
            throw new BadRequestException(string.Empty, "Master Password reset is required, but not provided.");
        }

        await _organizationService.AcceptUserAsync(organizationUserId, user, model.Token, _userService);

        if (useMasterPasswordPolicy)
        {
            await _organizationService.UpdateUserResetPasswordEnrollmentAsync(orgId, user.Id, model.ResetPasswordKey, user.Id);
        }
    }

    [HttpPost("{id}/confirm")]
    public async Task Confirm(string orgId, string id, [FromBody] OrganizationUserConfirmRequestModel model)
    {
        var orgGuidId = new Guid(orgId);
        if (!await _currentContext.ManageUsers(orgGuidId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User);
        var result = await _organizationService.ConfirmUserAsync(orgGuidId, new Guid(id), model.Key, userId.Value,
            _userService);
    }

    [HttpPost("confirm")]
    public async Task<ListResponseModel<OrganizationUserBulkResponseModel>> BulkConfirm(string orgId,
        [FromBody] OrganizationUserBulkConfirmRequestModel model)
    {
        var orgGuidId = new Guid(orgId);
        if (!await _currentContext.ManageUsers(orgGuidId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User);
        var results = await _organizationService.ConfirmUsersAsync(orgGuidId, model.ToDictionary(), userId.Value,
            _userService);

        return new ListResponseModel<OrganizationUserBulkResponseModel>(results.Select(r =>
            new OrganizationUserBulkResponseModel(r.Item1.Id, r.Item2)));
    }

    [HttpPost("public-keys")]
    public async Task<ListResponseModel<OrganizationUserPublicKeyResponseModel>> UserPublicKeys(string orgId, [FromBody] OrganizationUserBulkRequestModel model)
    {
        var orgGuidId = new Guid(orgId);
        if (!await _currentContext.ManageUsers(orgGuidId))
        {
            throw new NotFoundException();
        }

        var result = await _organizationUserRepository.GetManyPublicKeysByOrganizationUserAsync(orgGuidId, model.Ids);
        var responses = result.Select(r => new OrganizationUserPublicKeyResponseModel(r.Id, r.UserId, r.PublicKey)).ToList();
        return new ListResponseModel<OrganizationUserPublicKeyResponseModel>(responses);
    }

    [HttpPut("{id}")]
    [HttpPost("{id}")]
    public async Task Put(string orgId, string id, [FromBody] OrganizationUserUpdateRequestModel model)
    {
        var orgGuidId = new Guid(orgId);
        if (!await _currentContext.ManageUsers(orgGuidId))
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
    public async Task PutGroups(string orgId, string id, [FromBody] OrganizationUserUpdateGroupsRequestModel model)
    {
        var orgGuidId = new Guid(orgId);
        if (!await _currentContext.ManageUsers(orgGuidId))
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
    public async Task PutResetPasswordEnrollment(string orgId, string userId, [FromBody] OrganizationUserResetPasswordEnrollmentRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        if (model.ResetPasswordKey != null && !await _userService.VerifySecretAsync(user, model.Secret))
        {
            await Task.Delay(2000);
            throw new BadRequestException("MasterPasswordHash", "Invalid password.");
        }
        else
        {
            var callingUserId = user.Id;
            await _organizationService.UpdateUserResetPasswordEnrollmentAsync(
               new Guid(orgId), new Guid(userId), model.ResetPasswordKey, callingUserId);
        }
    }

    [HttpPut("{id}/reset-password")]
    public async Task PutResetPassword(string orgId, string id, [FromBody] OrganizationUserResetPasswordRequestModel model)
    {

        var orgGuidId = new Guid(orgId);

        // Calling user must have Manage Reset Password permission
        if (!await _currentContext.ManageResetPassword(orgGuidId))
        {
            throw new NotFoundException();
        }

        // Get the users role, since provider users aren't a member of the organization we use the owner check
        var orgUserType = await _currentContext.OrganizationOwner(orgGuidId)
            ? OrganizationUserType.Owner
            : _currentContext.Organizations?.FirstOrDefault(o => o.Id == orgGuidId)?.Type;
        if (orgUserType == null)
        {
            throw new NotFoundException();
        }

        var result = await _userService.AdminResetPasswordAsync(orgUserType.Value, orgGuidId, new Guid(id), model.NewMasterPasswordHash, model.Key);
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
        if (!await _currentContext.ManageUsers(orgGuidId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User);
        await _organizationService.DeleteUserAsync(orgGuidId, new Guid(id), userId.Value);
    }

    [HttpDelete("")]
    [HttpPost("delete")]
    public async Task<ListResponseModel<OrganizationUserBulkResponseModel>> BulkDelete(string orgId, [FromBody] OrganizationUserBulkRequestModel model)
    {
        var orgGuidId = new Guid(orgId);
        if (!await _currentContext.ManageUsers(orgGuidId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User);
        var result = await _organizationService.DeleteUsersAsync(orgGuidId, model.Ids, userId.Value);
        return new ListResponseModel<OrganizationUserBulkResponseModel>(result.Select(r =>
            new OrganizationUserBulkResponseModel(r.Item1.Id, r.Item2)));
    }

    [Obsolete("2022-07-22 Moved to {id}/revoke endpoint")]
    [HttpPatch("{id}/deactivate")]
    [HttpPut("{id}/deactivate")]
    public async Task Deactivate(Guid orgId, Guid id)
    {
        await RevokeAsync(orgId, id);
    }

    [Obsolete("2022-07-22 Moved to /revoke endpoint")]
    [HttpPatch("deactivate")]
    [HttpPut("deactivate")]
    public async Task<ListResponseModel<OrganizationUserBulkResponseModel>> BulkDeactivate(Guid orgId, [FromBody] OrganizationUserBulkRequestModel model)
    {
        return await BulkRevokeAsync(orgId, model);
    }

    [Obsolete("2022-07-22 Moved to {id}/restore endpoint")]
    [HttpPatch("{id}/activate")]
    [HttpPut("{id}/activate")]
    public async Task Activate(Guid orgId, Guid id)
    {
        await RestoreAsync(orgId, id);
    }

    [Obsolete("2022-07-22 Moved to /restore endpoint")]
    [HttpPatch("activate")]
    [HttpPut("activate")]
    public async Task<ListResponseModel<OrganizationUserBulkResponseModel>> BulkActivate(Guid orgId, [FromBody] OrganizationUserBulkRequestModel model)
    {
        return await BulkRestoreAsync(orgId, model);
    }

    [HttpPatch("{id}/revoke")]
    [HttpPut("{id}/revoke")]
    public async Task RevokeAsync(Guid orgId, Guid id)
    {
        await RestoreOrRevokeUserAsync(orgId, id, _organizationService.RevokeUserAsync);
    }

    [HttpPatch("revoke")]
    [HttpPut("revoke")]
    public async Task<ListResponseModel<OrganizationUserBulkResponseModel>> BulkRevokeAsync(Guid orgId, [FromBody] OrganizationUserBulkRequestModel model)
    {
        return await RestoreOrRevokeUsersAsync(orgId, model, _organizationService.RevokeUsersAsync);
    }

    [HttpPatch("{id}/restore")]
    [HttpPut("{id}/restore")]
    public async Task RestoreAsync(Guid orgId, Guid id)
    {
        await RestoreOrRevokeUserAsync(orgId, id, (orgUser, userId) => _organizationService.RestoreUserAsync(orgUser, userId, _userService));
    }

    [HttpPatch("restore")]
    [HttpPut("restore")]
    public async Task<ListResponseModel<OrganizationUserBulkResponseModel>> BulkRestoreAsync(Guid orgId, [FromBody] OrganizationUserBulkRequestModel model)
    {
        return await RestoreOrRevokeUsersAsync(orgId, model, (orgId, orgUserIds, restoringUserId) => _organizationService.RestoreUsersAsync(orgId, orgUserIds, restoringUserId, _userService));
    }

    private async Task RestoreOrRevokeUserAsync(
        Guid orgId,
        Guid id,
        Func<OrganizationUser, Guid?, Task> statusAction)
    {
        if (!await _currentContext.ManageUsers(orgId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User);
        var orgUser = await _organizationUserRepository.GetByIdAsync(id);
        if (orgUser == null || orgUser.OrganizationId != orgId)
        {
            throw new NotFoundException();
        }

        await statusAction(orgUser, userId);
    }

    private async Task<ListResponseModel<OrganizationUserBulkResponseModel>> RestoreOrRevokeUsersAsync(
        Guid orgId,
        OrganizationUserBulkRequestModel model,
        Func<Guid, IEnumerable<Guid>, Guid?, Task<List<Tuple<OrganizationUser, string>>>> statusAction)
    {
        if (!await _currentContext.ManageUsers(orgId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User);
        var result = await statusAction(orgId, model.Ids, userId.Value);
        return new ListResponseModel<OrganizationUserBulkResponseModel>(result.Select(r =>
            new OrganizationUserBulkResponseModel(r.Item1.Id, r.Item2)));
    }
}
