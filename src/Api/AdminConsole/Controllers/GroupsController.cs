// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.AdminConsole.Authorization;
using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Api.AdminConsole.Models.Response;
using Bit.Api.Models.Response;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Authorization;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

[Route("organizations/{orgId}/groups")]
[Authorize("Application")]
public class GroupsController : Controller
{
    private readonly IGroupRepository _groupRepository;
    private readonly IGroupService _groupService;
    private readonly IDeleteGroupCommand _deleteGroupCommand;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ICreateGroupCommand _createGroupCommand;
    private readonly IUpdateGroupCommand _updateGroupCommand;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICollectionRepository _collectionRepository;

    public GroupsController(
        IGroupRepository groupRepository,
        IGroupService groupService,
        IOrganizationRepository organizationRepository,
        ICreateGroupCommand createGroupCommand,
        IUpdateGroupCommand updateGroupCommand,
        IDeleteGroupCommand deleteGroupCommand,
        IAuthorizationService authorizationService,
        ICollectionRepository collectionRepository)
    {
        _groupRepository = groupRepository;
        _groupService = groupService;
        _organizationRepository = organizationRepository;
        _createGroupCommand = createGroupCommand;
        _updateGroupCommand = updateGroupCommand;
        _deleteGroupCommand = deleteGroupCommand;
        _authorizationService = authorizationService;
        _collectionRepository = collectionRepository;
    }

    [HttpGet("{id}")]
    [Authorize<ManageGroupsRequirement>]
    public async Task<GroupResponseModel> Get(Guid orgId, Guid id)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null || group.OrganizationId != orgId)
        {
            throw new NotFoundException();
        }

        return new GroupResponseModel(group);
    }

    [HttpGet("{id}/details")]
    [Authorize<ManageGroupsRequirement>]
    public async Task<GroupDetailsResponseModel> GetDetails(Guid orgId, Guid id)
    {
        var groupDetails = await _groupRepository.GetByIdWithCollectionsAsync(id);
        if (groupDetails?.Item1 == null || groupDetails.Item1.OrganizationId != orgId)
        {
            throw new NotFoundException();
        }

        return new GroupDetailsResponseModel(groupDetails.Item1, groupDetails.Item2);
    }

    [HttpGet("")]
    [Authorize<MemberOrProviderRequirement>]
    public async Task<ListResponseModel<GroupResponseModel>> GetOrganizationGroups(Guid orgId)
    {
        var groups = await _groupRepository.GetManyByOrganizationIdAsync(orgId);
        var responses = groups.Select(g => new GroupResponseModel(g));
        return new ListResponseModel<GroupResponseModel>(responses);
    }

    [HttpGet("details")]
    [Authorize<ManageUsersOrGroupsRequirement>]
    public async Task<ListResponseModel<GroupDetailsResponseModel>> GetOrganizationGroupDetails(Guid orgId)
    {
        var groups = await _groupRepository.GetManyWithCollectionsByOrganizationIdAsync(orgId);
        var responses = groups.Select(g => new GroupDetailsResponseModel(g.Item1, g.Item2));
        return new ListResponseModel<GroupDetailsResponseModel>(responses);
    }

    [HttpGet("{id}/users")]
    [Authorize<ManageGroupsRequirement>]
    public async Task<IEnumerable<Guid>> GetUsers(Guid orgId, Guid id)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null || group.OrganizationId != orgId)
        {
            throw new NotFoundException();
        }

        var groupIds = await _groupRepository.GetManyUserIdsByIdAsync(id);
        return groupIds;
    }

    [HttpPost("")]
    [Authorize<ManageGroupsRequirement>]
    public async Task<GroupResponseModel> Post(Guid orgId, [FromBody] GroupRequestModel model)
    {
        // Check the user has permission to grant access to the collections for the new group
        if (model.Collections?.Any() == true)
        {
            var collections = await _collectionRepository.GetManyByManyIdsAsync(model.Collections.Select(a => a.Id));
            var authorized =
                (await _authorizationService.AuthorizeAsync(User, collections, BulkCollectionOperations.ModifyGroupAccess))
                .Succeeded;
            if (!authorized)
            {
                throw new NotFoundException();
            }
        }

        var organization = await _organizationRepository.GetByIdAsync(orgId);
        var group = model.ToGroup(orgId);
        await _createGroupCommand.CreateGroupAsync(group, organization, model.Collections?.Select(c => c.ToSelectionReadOnly()).ToList(), model.Users);

        return new GroupResponseModel(group);
    }

    [HttpPut("{id}")]
    [Authorize<ManageGroupsRequirement>]
    public async Task<GroupResponseModel> Put(Guid orgId, Guid id, [FromBody] GroupRequestModel model)
    {
        var (group, currentAccess) = await _groupRepository.GetByIdWithCollectionsAsync(id);
        if (group == null || group.OrganizationId != orgId)
        {
            throw new NotFoundException();
        }

        // Authorization check:
        // If admins are not allowed access to all collections, you cannot add yourself to a group.
        var groupUserAssignment = new GroupUserAssignmentContext(orgId, model.Users, GroupId: id);
        if (!(await _authorizationService.AuthorizeAsync(User, groupUserAssignment, GroupUserOperations.AssignUsers)).Succeeded)
        {
            throw new BadRequestException("You cannot add yourself to groups.");
        }

        // Authorization check:
        // You must have authorization to ModifyUserAccess for all collections being saved
        var postedCollections = await _collectionRepository
            .GetManyByManyIdsAsync(model.Collections.Select(c => c.Id));
        foreach (var collection in postedCollections)
        {
            if (!(await _authorizationService.AuthorizeAsync(User, collection,
                    BulkCollectionOperations.ModifyGroupAccess))
                .Succeeded)
            {
                throw new NotFoundException();
            }
        }

        // The client only sends collections that the saving user has permissions to edit.
        // We need to combine these with collections that the user doesn't have permissions for, so that we don't
        // accidentally overwrite those
        var currentCollections = await _collectionRepository
            .GetManyByManyIdsAsync(currentAccess.Select(cas => cas.Id));

        var readonlyCollectionIds = new HashSet<Guid>();
        foreach (var collection in currentCollections)
        {
            if (!(await _authorizationService.AuthorizeAsync(User, collection, BulkCollectionOperations.ModifyGroupAccess))
                .Succeeded)
            {
                readonlyCollectionIds.Add(collection.Id);
            }
        }

        var editedCollectionAccess = model.Collections
            .Select(c => c.ToSelectionReadOnly());
        var readonlyCollectionAccess = currentAccess
            .Where(ca => readonlyCollectionIds.Contains(ca.Id));
        var collectionsToSave = editedCollectionAccess
            .Concat(readonlyCollectionAccess)
            .ToList();

        var organization = await _organizationRepository.GetByIdAsync(orgId);

        await _updateGroupCommand.UpdateGroupAsync(model.ToGroup(group), organization, collectionsToSave, model.Users);
        return new GroupResponseModel(group);
    }

    [HttpPost("{id}")]
    [Obsolete("This endpoint is deprecated. Use PUT method instead")]
    [Authorize<ManageGroupsRequirement>]
    public async Task<GroupResponseModel> PostPut(Guid orgId, Guid id, [FromBody] GroupRequestModel model)
    {
        return await Put(orgId, id, model);
    }

    [HttpDelete("{id}")]
    [Authorize<ManageGroupsRequirement>]
    public async Task Delete(Guid orgId, Guid id)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null || group.OrganizationId != orgId)
        {
            throw new NotFoundException();
        }

        await _deleteGroupCommand.DeleteAsync(group);
    }

    [HttpPost("{id}/delete")]
    [Obsolete("This endpoint is deprecated. Use DELETE method instead")]
    [Authorize<ManageGroupsRequirement>]
    public async Task PostDelete(Guid orgId, Guid id)
    {
        await Delete(orgId, id);
    }

    [HttpDelete("")]
    [Authorize<ManageGroupsRequirement>]
    public async Task BulkDelete(Guid orgId, [FromBody] GroupBulkRequestModel model)
    {
        var groups = await _groupRepository.GetManyByManyIds(model.Ids);

        foreach (var group in groups)
        {
            if (group.OrganizationId != orgId)
            {
                throw new NotFoundException();
            }
        }

        await _deleteGroupCommand.DeleteManyAsync(groups);
    }

    [HttpPost("delete")]
    [Obsolete("This endpoint is deprecated. Use DELETE method instead")]
    [Authorize<ManageGroupsRequirement>]
    public async Task PostBulkDelete(Guid orgId, [FromBody] GroupBulkRequestModel model)
    {
        await BulkDelete(orgId, model);
    }

    [HttpDelete("{id}/user/{orgUserId}")]
    [Authorize<ManageGroupsRequirement>]
    public async Task DeleteUser(Guid orgId, Guid id, Guid orgUserId)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null || group.OrganizationId != orgId)
        {
            throw new NotFoundException();
        }

        await _groupService.DeleteUserAsync(group, orgUserId);
    }

    [HttpPost("{id}/delete-user/{orgUserId}")]
    [Obsolete("This endpoint is deprecated. Use DELETE method instead")]
    [Authorize<ManageGroupsRequirement>]
    public async Task PostDeleteUser(Guid orgId, Guid id, Guid orgUserId)
    {
        await DeleteUser(orgId, id, orgUserId);
    }
}
