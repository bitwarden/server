using System.Net;
using Bit.Api.Models.Public.Request;
using Bit.Api.Models.Public.Response;
using Bit.Core.Context;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Public.Controllers;

[Route("public/members")]
[Authorize("Organization")]
public class MembersController : Controller
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IOrganizationService _organizationService;
    private readonly IUserService _userService;
    private readonly ICurrentContext _currentContext;

    public MembersController(
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        IOrganizationService organizationService,
        IUserService userService,
        ICurrentContext currentContext)
    {
        _organizationUserRepository = organizationUserRepository;
        _groupRepository = groupRepository;
        _organizationService = organizationService;
        _userService = userService;
        _currentContext = currentContext;
    }

    /// <summary>
    /// Retrieve a member.
    /// </summary>
    /// <remarks>
    /// Retrieves the details of an existing member of the organization. You need only supply the
    /// unique member identifier that was returned upon member creation.
    /// </remarks>
    /// <param name="id">The identifier of the member to be retrieved.</param>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(MemberResponseModel), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> Get(Guid id)
    {
        var userDetails = await _organizationUserRepository.GetDetailsByIdWithCollectionsAsync(id);
        var orgUser = userDetails?.Item1;
        if (orgUser == null || orgUser.OrganizationId != _currentContext.OrganizationId)
        {
            return new NotFoundResult();
        }
        var response = new MemberResponseModel(orgUser, await _userService.TwoFactorIsEnabledAsync(orgUser),
            userDetails.Item2);
        return new JsonResult(response);
    }

    /// <summary>
    /// Retrieve a member's group ids
    /// </summary>
    /// <remarks>
    /// Retrieves the unique identifiers for all groups that are associated with this member. You need only
    /// supply the unique member identifier that was returned upon member creation.
    /// </remarks>
    /// <param name="id">The identifier of the member to be retrieved.</param>
    [HttpGet("{id}/group-ids")]
    [ProducesResponseType(typeof(HashSet<Guid>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> GetGroupIds(Guid id)
    {
        var orgUser = await _organizationUserRepository.GetByIdAsync(id);
        if (orgUser == null || orgUser.OrganizationId != _currentContext.OrganizationId)
        {
            return new NotFoundResult();
        }
        var groupIds = await _groupRepository.GetManyIdsByUserIdAsync(id);
        return new JsonResult(groupIds);
    }

    /// <summary>
    /// List all members.
    /// </summary>
    /// <remarks>
    /// Returns a list of your organization's members.
    /// Member objects listed in this call do not include information about their associated collections.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(ListResponseModel<MemberResponseModel>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> List()
    {
        var users = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(
            _currentContext.OrganizationId.Value);
        // TODO: Get all CollectionUser associations for the organization and marry them up here for the response.
        var memberResponsesTasks = users.Select(async u => new MemberResponseModel(u,
            await _userService.TwoFactorIsEnabledAsync(u), null));
        var memberResponses = await Task.WhenAll(memberResponsesTasks);
        var response = new ListResponseModel<MemberResponseModel>(memberResponses);
        return new JsonResult(response);
    }

    /// <summary>
    /// Create a member.
    /// </summary>
    /// <remarks>
    /// Creates a new member object by inviting a user to the organization.
    /// </remarks>
    /// <param name="model">The request model.</param>
    [HttpPost]
    [ProducesResponseType(typeof(MemberResponseModel), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponseModel), (int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> Post([FromBody] MemberCreateRequestModel model)
    {
        var associations = model.Collections?.Select(c => c.ToSelectionReadOnly());
        var invite = new OrganizationUserInvite
        {
            Emails = new List<string> { model.Email },
            Type = model.Type.Value,
            AccessAll = model.AccessAll.Value,
            Collections = associations
        };
        var user = await _organizationService.InviteUserAsync(_currentContext.OrganizationId.Value, null,
            model.Email, model.Type.Value, model.AccessAll.Value, model.ExternalId, associations);
        var response = new MemberResponseModel(user, associations);
        return new JsonResult(response);
    }

    /// <summary>
    /// Update a member.
    /// </summary>
    /// <remarks>
    /// Updates the specified member object. If a property is not provided,
    /// the value of the existing property will be reset.
    /// </remarks>
    /// <param name="id">The identifier of the member to be updated.</param>
    /// <param name="model">The request model.</param>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(MemberResponseModel), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponseModel), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> Put(Guid id, [FromBody] MemberUpdateRequestModel model)
    {
        var existingUser = await _organizationUserRepository.GetByIdAsync(id);
        if (existingUser == null || existingUser.OrganizationId != _currentContext.OrganizationId)
        {
            return new NotFoundResult();
        }
        var updatedUser = model.ToOrganizationUser(existingUser);
        var associations = model.Collections?.Select(c => c.ToSelectionReadOnly());
        await _organizationService.SaveUserAsync(updatedUser, null, associations);
        MemberResponseModel response = null;
        if (existingUser.UserId.HasValue)
        {
            var existingUserDetails = await _organizationUserRepository.GetDetailsByIdAsync(id);
            response = new MemberResponseModel(existingUserDetails,
                await _userService.TwoFactorIsEnabledAsync(existingUserDetails), associations);
        }
        else
        {
            response = new MemberResponseModel(updatedUser, associations);
        }
        return new JsonResult(response);
    }

    /// <summary>
    /// Update a member's groups.
    /// </summary>
    /// <remarks>
    /// Updates the specified member's group associations.
    /// </remarks>
    /// <param name="id">The identifier of the member to be updated.</param>
    /// <param name="model">The request model.</param>
    [HttpPut("{id}/group-ids")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponseModel), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> PutGroupIds(Guid id, [FromBody] UpdateGroupIdsRequestModel model)
    {
        var existingUser = await _organizationUserRepository.GetByIdAsync(id);
        if (existingUser == null || existingUser.OrganizationId != _currentContext.OrganizationId)
        {
            return new NotFoundResult();
        }
        await _organizationService.UpdateUserGroupsAsync(existingUser, model.GroupIds, null);
        return new OkResult();
    }

    /// <summary>
    /// Delete a member.
    /// </summary>
    /// <remarks>
    /// Permanently deletes a member from the organization. This cannot be undone.
    /// The user account will still remain. The user is only removed from the organization.
    /// </remarks>
    /// <param name="id">The identifier of the member to be deleted.</param>
    [HttpDelete("{id}")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await _organizationUserRepository.GetByIdAsync(id);
        if (user == null || user.OrganizationId != _currentContext.OrganizationId)
        {
            return new NotFoundResult();
        }
        await _organizationService.DeleteUserAsync(_currentContext.OrganizationId.Value, id, null);
        return new OkResult();
    }

    /// <summary>
    /// Re-invite a member.
    /// </summary>
    /// <remarks>
    /// Re-sends the invitation email to an organization member.
    /// </remarks>
    /// <param name="id">The identifier of the member to re-invite.</param>
    [HttpPost("{id}/reinvite")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponseModel), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> PostReinvite(Guid id)
    {
        var existingUser = await _organizationUserRepository.GetByIdAsync(id);
        if (existingUser == null || existingUser.OrganizationId != _currentContext.OrganizationId)
        {
            return new NotFoundResult();
        }
        await _organizationService.ResendInviteAsync(_currentContext.OrganizationId.Value, null, id);
        return new OkResult();
    }
}
