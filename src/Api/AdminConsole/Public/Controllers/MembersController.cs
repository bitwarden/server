using System.Net;
using Bit.Api.AdminConsole.Public.Models.Request;
using Bit.Api.AdminConsole.Public.Models.Response;
using Bit.Api.Models.Public.Response;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Context;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Public.Controllers;

[Route("public/members")]
[Authorize("Organization")]
public class MembersController : Controller
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IOrganizationService _organizationService;
    private readonly IUserService _userService;
    private readonly ICurrentContext _currentContext;
    private readonly IUpdateOrganizationUserCommand _updateOrganizationUserCommand;
    private readonly IUpdateOrganizationUserGroupsCommand _updateOrganizationUserGroupsCommand;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IPaymentService _paymentService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ITwoFactorIsEnabledQuery _twoFactorIsEnabledQuery;
    private readonly IRemoveOrganizationUserCommand _removeOrganizationUserCommand;

    public MembersController(
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        IOrganizationService organizationService,
        IUserService userService,
        ICurrentContext currentContext,
        IUpdateOrganizationUserCommand updateOrganizationUserCommand,
        IUpdateOrganizationUserGroupsCommand updateOrganizationUserGroupsCommand,
        IApplicationCacheService applicationCacheService,
        IPaymentService paymentService,
        IOrganizationRepository organizationRepository,
        ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
        IRemoveOrganizationUserCommand removeOrganizationUserCommand)
    {
        _organizationUserRepository = organizationUserRepository;
        _groupRepository = groupRepository;
        _organizationService = organizationService;
        _userService = userService;
        _currentContext = currentContext;
        _updateOrganizationUserCommand = updateOrganizationUserCommand;
        _updateOrganizationUserGroupsCommand = updateOrganizationUserGroupsCommand;
        _applicationCacheService = applicationCacheService;
        _paymentService = paymentService;
        _organizationRepository = organizationRepository;
        _twoFactorIsEnabledQuery = twoFactorIsEnabledQuery;
        _removeOrganizationUserCommand = removeOrganizationUserCommand;
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
        var (orgUser, collections) = await _organizationUserRepository.GetDetailsByIdWithCollectionsAsync(id);
        if (orgUser == null || orgUser.OrganizationId != _currentContext.OrganizationId)
        {
            return new NotFoundResult();
        }
        var response = new MemberResponseModel(orgUser, await _userService.TwoFactorIsEnabledAsync(orgUser),
            collections);
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
        var organizationUserUserDetails = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(_currentContext.OrganizationId.Value);
        // TODO: Get all CollectionUser associations for the organization and marry them up here for the response.

        var orgUsersTwoFactorIsEnabled = await _twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(organizationUserUserDetails);
        var memberResponses = organizationUserUserDetails.Select(u =>
        {
            return new MemberResponseModel(u, orgUsersTwoFactorIsEnabled.FirstOrDefault(tuple => tuple.user == u).twoFactorIsEnabled, null);
        });
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
        var hasStandaloneSecretsManager = false;

        var organization = await _organizationRepository.GetByIdAsync(_currentContext.OrganizationId!.Value);

        if (organization != null)
        {
            hasStandaloneSecretsManager = await _paymentService.HasSecretsManagerStandalone(organization);
        }

        var invite = model.ToOrganizationUserInvite();

        invite.AccessSecretsManager = hasStandaloneSecretsManager;

        var user = await _organizationService.InviteUserAsync(_currentContext.OrganizationId.Value, null,
            systemUser: null, invite, model.ExternalId);
        var response = new MemberResponseModel(user, invite.Collections);
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
        var associations = model.Collections?.Select(c => c.ToCollectionAccessSelection()).ToList();
        await _updateOrganizationUserCommand.UpdateUserAsync(updatedUser, null, associations, model.Groups);
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
        await _updateOrganizationUserGroupsCommand.UpdateUserGroupsAsync(existingUser, model.GroupIds);
        return new OkResult();
    }

    /// <summary>
    /// Remove a member.
    /// </summary>
    /// <remarks>
    /// Permanently removes a member from the organization. This cannot be undone.
    /// The user account will still remain. The user is only removed from the organization.
    /// </remarks>
    /// <param name="id">The identifier of the member to be removed.</param>
    [HttpDelete("{id}")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> Remove(Guid id)
    {
        var user = await _organizationUserRepository.GetByIdAsync(id);
        if (user == null || user.OrganizationId != _currentContext.OrganizationId)
        {
            return new NotFoundResult();
        }
        await _removeOrganizationUserCommand.RemoveUserAsync(_currentContext.OrganizationId.Value, id, null);
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
