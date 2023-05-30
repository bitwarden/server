using Bit.Api.Models.Request;
using Bit.Api.Models.Response;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.AuthorizationHandlers;
using Bit.Core.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[Route("organizations/{orgId}/groups")]
[Authorize("Application")]
public class GroupsController : Controller
{
    private readonly IGroupRepository _groupRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ICreateGroupCommand _createGroupCommand;
    private readonly IUpdateGroupCommand _updateGroupCommand;
    private readonly IBitAuthorizationService _bitAuthorizationService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IEventService _eventService;

    public GroupsController(
        IGroupRepository groupRepository,
        IOrganizationRepository organizationRepository,
        ICreateGroupCommand createGroupCommand,
        IUpdateGroupCommand updateGroupCommand,
        IBitAuthorizationService bitAuthorizationService,
        IOrganizationUserRepository organizationUserRepository,
        IEventService eventService)
    {
        _groupRepository = groupRepository;
        _organizationRepository = organizationRepository;
        _createGroupCommand = createGroupCommand;
        _updateGroupCommand = updateGroupCommand;
        _bitAuthorizationService = bitAuthorizationService;
        _organizationUserRepository = organizationUserRepository;
        _eventService = eventService;
    }

    [HttpGet("{id}")]
    public async Task<GroupResponseModel> Get(Guid orgId, Guid id)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        await _bitAuthorizationService.AuthorizeOrThrowAsync(User, group, GroupOperations.Read);

        return new GroupResponseModel(group);
    }

    [HttpGet("{id}/details")]
    public async Task<GroupDetailsResponseModel> GetDetails(Guid orgId, Guid id)
    {
        var groupDetails = await _groupRepository.GetByIdWithCollectionsAsync(id);

        await _bitAuthorizationService.AuthorizeOrThrowAsync(User, groupDetails.Group, GroupOperations.Read);

        return new GroupDetailsResponseModel(groupDetails.Group, groupDetails.AccessSelection);
    }

    [HttpGet("")]
    public async Task<ListResponseModel<GroupDetailsResponseModel>> Get(Guid orgId)
    {
        await _bitAuthorizationService.AuthorizeOrThrowAsync(User, GroupOperations.ReadAllForOrganization(orgId));

        var groups = await _groupRepository.GetManyWithCollectionsByOrganizationIdAsync(orgId);
        var responses = groups.Select(g => new GroupDetailsResponseModel(g.Item1, g.Item2));
        return new ListResponseModel<GroupDetailsResponseModel>(responses);
    }

    [HttpGet("{id}/users")]
    public async Task<IEnumerable<Guid>> GetUsers(Guid orgId, Guid id)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        await _bitAuthorizationService.AuthorizeOrThrowAsync(User, group, GroupOperations.Read);

        var groupIds = await _groupRepository.GetManyUserIdsByIdAsync(id);
        return groupIds;
    }

    [HttpPost("")]
    public async Task<GroupResponseModel> Post(Guid orgId, [FromBody] GroupRequestModel model)
    {
        var group = model.ToGroup(orgId);
        await _bitAuthorizationService.AuthorizeOrThrowAsync(User, group, GroupOperations.Create);

        var organization = await _organizationRepository.GetByIdAsync(orgId);
        await _createGroupCommand.CreateGroupAsync(group, organization, model.Collections?.Select(c => c.ToSelectionReadOnly()), model.Users);

        return new GroupResponseModel(group);
    }

    [HttpPut("{id}")]
    [HttpPost("{id}")]
    public async Task<GroupResponseModel> Put(Guid orgId, Guid id, [FromBody] GroupRequestModel model)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        await _bitAuthorizationService.AuthorizeOrThrowAsync(User, group, GroupOperations.Update);

        var organization = await _organizationRepository.GetByIdAsync(orgId);
        await _updateGroupCommand.UpdateGroupAsync(model.ToGroup(group), organization, model.Collections?.Select(c => c.ToSelectionReadOnly()), model.Users);

        return new GroupResponseModel(group);
    }

    [HttpPut("{id}/users")]
    public async Task PutUsers(Guid orgId, Guid id, [FromBody] IEnumerable<Guid> model)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        await _bitAuthorizationService.AuthorizeOrThrowAsync(User, group, new[]
            {
                GroupUserOperations.Create,
                GroupUserOperations.Delete
            });

        await _groupRepository.UpdateUsersAsync(group.Id, model);
    }

    [HttpDelete("{id}")]
    [HttpPost("{id}/delete")]
    public async Task Delete(Guid orgId, Guid id)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        await _bitAuthorizationService.AuthorizeOrThrowAsync(User, group, GroupOperations.Delete);

        await _groupRepository.DeleteAsync(group);
        await _eventService.LogGroupEventAsync(group, EventType.Group_Deleted);
    }

    [HttpDelete("")]
    [HttpPost("delete")]
    public async Task BulkDelete([FromBody] GroupBulkRequestModel model)
    {
        var groups = await _groupRepository.GetManyByManyIds(model.Ids);

        foreach (var group in groups)
        {
            await _bitAuthorizationService.AuthorizeOrThrowAsync(User, group, GroupOperations.Delete);
        }

        await _groupRepository.DeleteManyAsync(groups.Select(g => g.Id));
        await _eventService.LogGroupEventsAsync(
            groups.Select(g =>
                (g, EventType.Group_Deleted, (EventSystemUser?)null, (DateTime?)DateTime.UtcNow)
            ));
    }

    [HttpDelete("{id}/user/{orgUserId}")]
    [HttpPost("{id}/delete-user/{orgUserId}")]
    public async Task Delete(Guid orgId, Guid id, Guid orgUserId)
    {
        // Verify that the group belongs to the organization before proceeding
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null || group.OrganizationId != orgId)
        {
            throw new NotFoundException();
        }

        var groupUser = await _groupRepository.GetGroupUserByGroupIdOrganizationUserId(id, orgUserId);
        await _bitAuthorizationService.AuthorizeOrThrowAsync(User, groupUser, GroupUserOperations.Delete);

        var orgUser = await _organizationUserRepository.GetByIdAsync(groupUser.OrganizationUserId);

        await _groupRepository.DeleteUserAsync(groupUser.GroupId, groupUser.OrganizationUserId);
        await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_UpdatedGroups);
    }
}
