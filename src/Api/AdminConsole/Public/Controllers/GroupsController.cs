// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Net;
using Bit.Api.AdminConsole.Public.Models.Request;
using Bit.Api.AdminConsole.Public.Models.Response;
using Bit.Api.Models.Public.Response;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Identity;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Public.Controllers;

[Route("public/groups")]
public class GroupsController : Controller
{
    private readonly IGroupRepository _groupRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ICurrentContext _currentContext;
    private readonly ICreateGroupCommand _createGroupCommand;
    private readonly IUpdateGroupCommand _updateGroupCommand;
    private readonly IScopedApiKeyGroupMemberValidator _scopedApiKeyGroupMemberValidator;
    private readonly TimeProvider _timeProvider;

    public GroupsController(
        IGroupRepository groupRepository,
        IOrganizationRepository organizationRepository,
        ICurrentContext currentContext,
        ICreateGroupCommand createGroupCommand,
        IUpdateGroupCommand updateGroupCommand,
        IScopedApiKeyGroupMemberValidator scopedApiKeyGroupMemberValidator,
        TimeProvider timeProvider)
    {
        _groupRepository = groupRepository;
        _organizationRepository = organizationRepository;
        _currentContext = currentContext;
        _createGroupCommand = createGroupCommand;
        _updateGroupCommand = updateGroupCommand;
        _scopedApiKeyGroupMemberValidator = scopedApiKeyGroupMemberValidator;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Retrieve a group.
    /// </summary>
    /// <remarks>
    /// Retrieves the details of an existing group. You need only supply the unique group identifier
    /// that was returned upon group creation.
    /// </remarks>
    /// <param name="id">The identifier of the group to be retrieved.</param>
    [Authorize(Policies.OrganizationGroupsRead)]
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(GroupResponseModel), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> Get(Guid id)
    {
        var groupDetails = await _groupRepository.GetByIdWithCollectionsAsync(id);
        var group = groupDetails?.Item1;
        if (group == null || group.OrganizationId != _currentContext.OrganizationId)
        {
            return new NotFoundResult();
        }
        var response = new GroupResponseModel(group, groupDetails.Item2);
        return new JsonResult(response);
    }

    /// <summary>
    /// Retrieve a groups's member ids
    /// </summary>
    /// <remarks>
    /// Retrieves the unique identifiers for all members that are associated with this group. You need only
    /// supply the unique group identifier that was returned upon group creation.
    /// </remarks>
    /// <param name="id">The identifier of the group to be retrieved.</param>
    [Authorize(Policies.OrganizationGroupsRead)]
    [HttpGet("{id}/member-ids")]
    [ProducesResponseType(typeof(HashSet<Guid>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> GetMemberIds(Guid id)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null || group.OrganizationId != _currentContext.OrganizationId)
        {
            return new NotFoundResult();
        }
        var orgUserIds = await _groupRepository.GetManyUserIdsByIdAsync(id);
        return new JsonResult(orgUserIds);
    }

    /// <summary>
    /// List all groups.
    /// </summary>
    /// <remarks>
    /// Returns a list of your organization's groups.
    /// Group objects listed in this call include information about their associated collections.
    /// </remarks>
    [Authorize(Policies.OrganizationGroupsRead)]
    [HttpGet]
    [ProducesResponseType(typeof(ListResponseModel<GroupResponseModel>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> List()
    {
        var groups = await _groupRepository.GetManyWithCollectionsByOrganizationIdAsync(_currentContext.OrganizationId.Value);
        var groupResponses = groups.Select(g => new GroupResponseModel(g.Item1, g.Item2));
        var response = new ListResponseModel<GroupResponseModel>(groupResponses);
        return new JsonResult(response);
    }

    /// <summary>
    /// Create a group.
    /// </summary>
    /// <remarks>
    /// Creates a new group object.
    /// </remarks>
    /// <param name="model">The request model.</param>
    [Authorize(Policies.OrganizationGroupsWrite)]
    [HttpPost]
    [ProducesResponseType(typeof(GroupResponseModel), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponseModel), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> Post([FromBody] GroupCreateUpdateRequestModel model)
    {
        var group = model.ToGroup(_currentContext.OrganizationId.Value);
        var organization = await _organizationRepository.GetByIdAsync(_currentContext.OrganizationId.Value);
        var associations = model.Collections?.Select(c => c.ToCollectionAccessSelection()).ToList();
        await _createGroupCommand.CreateGroupAsync(group, organization, associations);
        var response = new GroupResponseModel(group, associations);
        return new JsonResult(response);
    }

    /// <summary>
    /// Update a group.
    /// </summary>
    /// <remarks>
    /// Updates the specified group object. If a property is not provided,
    /// the value of the existing property will be reset.
    /// </remarks>
    /// <param name="id">The identifier of the group to be updated.</param>
    /// <param name="model">The request model.</param>
    [Authorize(Policies.OrganizationGroupsWrite)]
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(GroupResponseModel), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponseModel), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> Put(Guid id, [FromBody] GroupCreateUpdateRequestModel model)
    {
        var existingGroup = await _groupRepository.GetByIdAsync(id);
        if (existingGroup == null || existingGroup.OrganizationId != _currentContext.OrganizationId)
        {
            return new NotFoundResult();
        }

        var updatedGroup = model.ToGroup(existingGroup);
        var organization = await _organizationRepository.GetByIdAsync(_currentContext.OrganizationId.Value);
        var associations = model.Collections?.Select(c => c.ToCollectionAccessSelection()).ToList();
        await _updateGroupCommand.UpdateGroupAsync(updatedGroup, organization, associations);
        var response = new GroupResponseModel(updatedGroup, associations);
        return new JsonResult(response);
    }

    /// <summary>
    /// Update a group's members.
    /// </summary>
    /// <remarks>
    /// Updates the specified group's member associations.
    /// </remarks>
    /// <param name="id">The identifier of the group to be updated.</param>
    /// <param name="model">The request model.</param>
    [Authorize(Policies.OrganizationGroupsWrite)]
    [HttpPut("{id}/member-ids")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponseModel), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> PutMemberIds(Guid id, [FromBody] UpdateMemberIdsRequestModel model)
    {
        var existingGroup = await _groupRepository.GetByIdAsync(id);
        if (existingGroup == null || existingGroup.OrganizationId != _currentContext.OrganizationId)
        {
            return new NotFoundResult();
        }
        await ValidateScopedApiKeyMemberChangeAsync(existingGroup.OrganizationId, existingGroup.Id, model.MemberIds ?? []);
        await _groupRepository.UpdateUsersAsync(existingGroup.Id, model.MemberIds, _timeProvider.GetUtcNow().UtcDateTime);
        return new OkResult();
    }

    /// <summary>
    /// Delete a group.
    /// </summary>
    /// <remarks>
    /// Permanently deletes a group. This cannot be undone.
    /// </remarks>
    /// <param name="id">The identifier of the group to be deleted.</param>
    [Authorize(Policies.OrganizationGroupsWrite)]
    [HttpDelete("{id}")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null || group.OrganizationId != _currentContext.OrganizationId)
        {
            return new NotFoundResult();
        }
        await ValidateScopedApiKeyMemberChangeAsync(group.OrganizationId, group.Id, []);
        await _groupRepository.DeleteAsync(group);
        return new OkResult();
    }

    private async Task ValidateScopedApiKeyMemberChangeAsync(Guid organizationId, Guid groupId, IEnumerable<Guid> memberIds)
    {
        var error = await _scopedApiKeyGroupMemberValidator.ValidateAsync(organizationId, groupId, memberIds);
        if (error is not null)
        {
            throw new BadRequestException(error.Message);
        }
    }
}
