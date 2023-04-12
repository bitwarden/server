using Bit.Api.Models.Request;
using Bit.Api.Models.Response;
using Bit.Core.Context;
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
    private readonly IGroupService _groupService;
    private readonly IDeleteGroupCommand _deleteGroupCommand;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ICurrentContext _currentContext;
    private readonly ICreateGroupCommand _createGroupCommand;
    private readonly IUpdateGroupCommand _updateGroupCommand;
    private readonly IAuthorizationService _authorizationService;

    public GroupsController(
        IGroupRepository groupRepository,
        IGroupService groupService,
        IOrganizationRepository organizationRepository,
        ICurrentContext currentContext,
        ICreateGroupCommand createGroupCommand,
        IUpdateGroupCommand updateGroupCommand,
        IDeleteGroupCommand deleteGroupCommand,
        IAuthorizationService authorizationService)
    {
        _groupRepository = groupRepository;
        _groupService = groupService;
        _organizationRepository = organizationRepository;
        _currentContext = currentContext;
        _createGroupCommand = createGroupCommand;
        _updateGroupCommand = updateGroupCommand;
        _deleteGroupCommand = deleteGroupCommand;
        _authorizationService = authorizationService;
    }

    [HttpGet("{id}")]
    public async Task<GroupResponseModel> Get(Guid orgId, Guid id)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null)
        {
            throw new NotFoundException();
        }

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, group, GroupOperations.Read);
        if (!authorizationResult.Succeeded)
        {
            throw new ResourceAuthorizationFailedException();
        }

        return new GroupResponseModel(group);
    }

    [HttpGet("{id}/details")]
    public async Task<GroupDetailsResponseModel> GetDetails(Guid orgId, Guid id)
    {
        var groupDetails = await _groupRepository.GetByIdWithCollectionsAsync(id);
        if (groupDetails.group == null)
        {
            throw new NotFoundException();
        }

        var authorizationResult =
            await _authorizationService.AuthorizeAsync(User, groupDetails.group, GroupOperations.Read);
        if (!authorizationResult.Succeeded)
        {
            throw new ResourceAuthorizationFailedException();
        }

        return new GroupDetailsResponseModel(groupDetails.Item1, groupDetails.Item2);
    }

    [HttpGet("")]
    public async Task<ListResponseModel<GroupDetailsResponseModel>> Get(Guid orgId)
    {
        var org = _currentContext.GetOrganization(orgId);

        var authorizationResult =
            await _authorizationService.AuthorizeAsync(User, org, OrganizationOperations.ReadAllGroups);
        if (!authorizationResult.Succeeded)
        {
            throw new ResourceAuthorizationFailedException();
        }

        var groups = await _groupRepository.GetManyWithCollectionsByOrganizationIdAsync(orgId);
        var responses = groups.Select(g => new GroupDetailsResponseModel(g.Item1, g.Item2));
        return new ListResponseModel<GroupDetailsResponseModel>(responses);
    }

    [HttpGet("{id}/users")]
    public async Task<IEnumerable<Guid>> GetUsers(Guid orgId, Guid id)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null)
        {
            throw new NotFoundException();
        }

        var authorizationResult =
            await _authorizationService.AuthorizeAsync(User, group, GroupOperations.Read);
        if (!authorizationResult.Succeeded)
        {
            throw new ResourceAuthorizationFailedException();
        }

        var groupIds = await _groupRepository.GetManyUserIdsByIdAsync(id);
        return groupIds;
    }

    [HttpPost("")]
    public async Task<GroupResponseModel> Post(Guid orgId, [FromBody] GroupRequestModel model)
    {
        var organization = await _organizationRepository.GetByIdAsync(orgId);
        var group = model.ToGroup(orgId);

        var authorizationResult =
            await _authorizationService.AuthorizeAsync(User, group, GroupOperations.Create);
        if (!authorizationResult.Succeeded)
        {
            throw new ResourceAuthorizationFailedException();
        }

        await _createGroupCommand.CreateGroupAsync(group, organization, model.Collections?.Select(c => c.ToSelectionReadOnly()), model.Users);

        return new GroupResponseModel(group);
    }

    [HttpPut("{id}")]
    [HttpPost("{id}")]
    public async Task<GroupResponseModel> Put(Guid orgId, Guid id, [FromBody] GroupRequestModel model)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null)
        {
            throw new NotFoundException();
        }

        var authorizationResult =
            await _authorizationService.AuthorizeAsync(User, group, GroupOperations.Update);
        if (!authorizationResult.Succeeded)
        {
            throw new ResourceAuthorizationFailedException();
        }

        var organization = await _organizationRepository.GetByIdAsync(orgId);

        await _updateGroupCommand.UpdateGroupAsync(model.ToGroup(group), organization, model.Collections?.Select(c => c.ToSelectionReadOnly()), model.Users);
        return new GroupResponseModel(group);
    }

    [HttpPut("{id}/users")]
    public async Task PutUsers(Guid orgId, Guid id, [FromBody] IEnumerable<Guid> model)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null)
        {
            throw new NotFoundException();
        }

        var authorizationResult =
            await _authorizationService.AuthorizeAsync(User, group, GroupOperations.AddUser);
        if (!authorizationResult.Succeeded)
        {
            throw new ResourceAuthorizationFailedException();
        }

        await _groupRepository.UpdateUsersAsync(group.Id, model);
    }

    [HttpDelete("{id}")]
    [HttpPost("{id}/delete")]
    public async Task Delete(Guid orgId, Guid id)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null)
        {
            throw new NotFoundException();
        }

        var authorizationResult =
            await _authorizationService.AuthorizeAsync(User, group, GroupOperations.Delete);
        if (!authorizationResult.Succeeded)
        {
            throw new ResourceAuthorizationFailedException();
        }

        await _deleteGroupCommand.DeleteAsync(group);
    }

    [HttpDelete("")]
    [HttpPost("delete")]
    public async Task BulkDelete([FromBody] GroupBulkRequestModel model)
    {
        var groups = await _groupRepository.GetManyByManyIds(model.Ids);

        foreach (var group in groups)
        {
            var authorizationResult = await _authorizationService.AuthorizeAsync(User, group, GroupOperations.Delete);
            if (!authorizationResult.Succeeded)
            {
                throw new ResourceAuthorizationFailedException();
            }
        }

        await _deleteGroupCommand.DeleteManyAsync(groups);
    }

    [HttpDelete("{id}/user/{orgUserId}")]
    [HttpPost("{id}/delete-user/{orgUserId}")]
    public async Task Delete(Guid orgId, Guid id, Guid orgUserId)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null)
        {
            throw new NotFoundException();
        }

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, group, GroupOperations.DeleteUser);
        if (!authorizationResult.Succeeded)
        {
            throw new ResourceAuthorizationFailedException();
        }

        await _groupService.DeleteUserAsync(group, orgUserId);
    }
}
