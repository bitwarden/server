using System.Net;
using Bit.Api.Models.Public.Request;
using Bit.Api.Models.Public.Response;
using Bit.Core.Context;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Public.Controllers;

[Route("public/groups")]
[Authorize("Organization")]
public class GroupsController : Controller
{
    private readonly IGroupRepository _groupRepository;
    private readonly IGroupService _groupService;
    private readonly ICurrentContext _currentContext;

    public GroupsController(
        IGroupRepository groupRepository,
        IGroupService groupService,
        ICurrentContext currentContext)
    {
        _groupRepository = groupRepository;
        _groupService = groupService;
        _currentContext = currentContext;
    }

    /// <summary>
    /// Retrieve a group.
    /// </summary>
    /// <remarks>
    /// Retrieves the details of an existing group. You need only supply the unique group identifier
    /// that was returned upon group creation.
    /// </remarks>
    /// <param name="id">The identifier of the group to be retrieved.</param>
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
    /// Group objects listed in this call do not include information about their associated collections.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(ListResponseModel<GroupResponseModel>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> List()
    {
        var groups = await _groupRepository.GetManyByOrganizationIdAsync(_currentContext.OrganizationId.Value);
        // TODO: Get all CollectionGroup associations for the organization and marry them up here for the response.
        var groupResponses = groups.Select(g => new GroupResponseModel(g, null));
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
    [HttpPost]
    [ProducesResponseType(typeof(GroupResponseModel), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponseModel), (int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> Post([FromBody] GroupCreateUpdateRequestModel model)
    {
        var group = model.ToGroup(_currentContext.OrganizationId.Value);
        var associations = model.Collections?.Select(c => c.ToSelectionReadOnly());
        await _groupService.SaveAsync(group, associations);
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
        var associations = model.Collections?.Select(c => c.ToSelectionReadOnly());
        await _groupService.SaveAsync(updatedGroup, associations);
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
        await _groupRepository.UpdateUsersAsync(existingGroup.Id, model.MemberIds);
        return new OkResult();
    }

    /// <summary>
    /// Delete a group.
    /// </summary>
    /// <remarks>
    /// Permanently deletes a group. This cannot be undone.
    /// </remarks>
    /// <param name="id">The identifier of the group to be deleted.</param>
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
        await _groupRepository.DeleteAsync(group);
        return new OkResult();
    }
}
