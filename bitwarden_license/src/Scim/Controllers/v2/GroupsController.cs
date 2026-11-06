// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Scim.Groups.Interfaces;
using Bit.Scim.Models;
using Bit.Scim.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Scim.Controllers.v2;

[Authorize("Scim")]
[Route("v2/{organizationId}/groups")]
[Produces("application/scim+json")]
[ExceptionHandlerFilter]
public class GroupsController : Controller
{
    private readonly IGroupRepository _groupRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IUserRepository _userRepository;
    private readonly IGetGroupsListQuery _getGroupsListQuery;
    private readonly IDeleteGroupCommand _deleteGroupCommand;
    private readonly IPatchGroupCommand _patchGroupCommand;
    private readonly IPostGroupCommand _postGroupCommand;
    private readonly IPutGroupCommand _putGroupCommand;

    public GroupsController(
        IGroupRepository groupRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IUserRepository userRepository,
        IGetGroupsListQuery getGroupsListQuery,
        IDeleteGroupCommand deleteGroupCommand,
        IPatchGroupCommand patchGroupCommand,
        IPostGroupCommand postGroupCommand,
        IPutGroupCommand putGroupCommand
        )
    {
        _groupRepository = groupRepository;
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _organizationUserRepository = organizationUserRepository;
        _getGroupsListQuery = getGroupsListQuery;
        _deleteGroupCommand = deleteGroupCommand;
        _patchGroupCommand = patchGroupCommand;
        _postGroupCommand = postGroupCommand;
        _putGroupCommand = putGroupCommand;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(
        [FromRoute] Guid organizationId,
        Guid id,
        [FromQuery] GetGroupsQueryParamModel model)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null || group.OrganizationId != organizationId)
        {
            throw new NotFoundException("Group not found.");
        }

        var groupResponse = new ScimGroupResponseModel(group);

        // Populate members with pagination
        var memberUserIds = await _groupRepository.GetManyUserIdsByIdAsync(id);
        if (memberUserIds.Any())
        {
            var paginatedMemberIds = memberUserIds
                .OrderBy(id => id)
                .Skip(model.MemberStartIndex - 1)
                .Take(model.MemberCount)
                .ToList();

            groupResponse.Members = await BuildMembersAsync(organizationId, paginatedMemberIds);
        }

        return Ok(groupResponse);
    }

    [HttpGet("")]
    public async Task<IActionResult> Get(
        [FromRoute] Guid organizationId,
        [FromQuery] GetGroupsQueryParamModel model)
    {
        var groupsListQueryResult = await _getGroupsListQuery.GetGroupsListAsync(organizationId, model);

        // Populate members for each group with pagination
        var groupResponseList = new List<ScimGroupResponseModel>();
        foreach (var group in groupsListQueryResult.groupList)
        {
            var groupResponse = new ScimGroupResponseModel(group);

            // Fetch and paginate members for this group
            var memberUserIds = await _groupRepository.GetManyUserIdsByIdAsync(group.Id);
            if (memberUserIds.Any())
            {
                var paginatedMemberIds = memberUserIds
                    .OrderBy(id => id)
                    .Skip(model.MemberStartIndex - 1)
                    .Take(model.MemberCount)
                    .ToList();

                groupResponse.Members = await BuildMembersAsync(organizationId, paginatedMemberIds);
            }

            groupResponseList.Add(groupResponse);
        }

        var scimListResponseModel = new ScimListResponseModel<ScimGroupResponseModel>
        {
            Resources = groupResponseList,
            ItemsPerPage = model.Count,
            TotalResults = groupsListQueryResult.totalResults,
            StartIndex = model.StartIndex,
        };
        return Ok(scimListResponseModel);
    }

    [HttpPost("")]
    public async Task<IActionResult> Post([FromRoute] Guid organizationId, [FromBody] ScimGroupRequestModel model)
    {
        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        var group = await _postGroupCommand.PostGroupAsync(organization, model);
        var scimGroupResponseModel = new ScimGroupResponseModel(group);
        return new CreatedResult(Url.Action(nameof(Get), new { group.OrganizationId, group.Id }), scimGroupResponseModel);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Put([FromRoute] Guid organizationId, Guid id, [FromBody] ScimGroupRequestModel model)
    {
        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        var group = await _putGroupCommand.PutGroupAsync(organization, id, model);
        var response = new ScimGroupResponseModel(group);

        return Ok(response);
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch([FromRoute] Guid organizationId, Guid id, [FromBody] ScimPatchModel model)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null || group.OrganizationId != organizationId)
        {
            throw new NotFoundException("Group not found.");
        }

        await _patchGroupCommand.PatchGroupAsync(group, model);
        return new NoContentResult();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete([FromRoute] Guid organizationId, Guid id)
    {
        await _deleteGroupCommand.DeleteGroupAsync(organizationId, id, EventSystemUser.SCIM);
        return new NoContentResult();
    }

    private async Task<List<ScimGroupResponseModel.GroupMemberReference>> BuildMembersAsync(
        Guid organizationId,
        IEnumerable<Guid> orgUserIds)
    {
        var orgUsers = (await _organizationUserRepository.GetManyAsync(orgUserIds)).ToList();
        var userIds = orgUsers.Where(ou => ou.UserId.HasValue).Select(ou => ou.UserId!.Value).Distinct().ToList();
        var emails = userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _userRepository.GetManyAsync(userIds)).ToDictionary(u => u.Id, u => u.Email);
        var usersUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/v2/{organizationId}/Users";
        return orgUsers
            .OrderBy(ou => ou.Id)
            .Select(ou => new ScimGroupResponseModel.GroupMemberReference
            {
                Value = ou.Id.ToString(),
                Display = ou.Email ?? (ou.UserId.HasValue && emails.TryGetValue(ou.UserId.Value, out var e) ? e : string.Empty),
                Ref = $"{usersUrl}/{ou.Id}",
                Type = "User"
            })
            .ToList();
    }
}
