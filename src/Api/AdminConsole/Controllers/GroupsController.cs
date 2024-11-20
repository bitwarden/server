using Bit.Api.AdminConsole.Models.Request;
using Bit.Api.AdminConsole.Models.Response;
using Bit.Api.Models.Response;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Authorization;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
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
    private readonly ICurrentContext _currentContext;
    private readonly ICreateGroupCommand _createGroupCommand;
    private readonly IUpdateGroupCommand _updateGroupCommand;
    private readonly IAuthorizationService _authorizationService;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IUserService _userService;
    private readonly IFeatureService _featureService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ICollectionRepository _collectionRepository;

    public GroupsController(
        IGroupRepository groupRepository,
        IGroupService groupService,
        IOrganizationRepository organizationRepository,
        ICurrentContext currentContext,
        ICreateGroupCommand createGroupCommand,
        IUpdateGroupCommand updateGroupCommand,
        IDeleteGroupCommand deleteGroupCommand,
        IAuthorizationService authorizationService,
        IApplicationCacheService applicationCacheService,
        IUserService userService,
        IFeatureService featureService,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        _groupRepository = groupRepository;
        _groupService = groupService;
        _organizationRepository = organizationRepository;
        _currentContext = currentContext;
        _createGroupCommand = createGroupCommand;
        _updateGroupCommand = updateGroupCommand;
        _deleteGroupCommand = deleteGroupCommand;
        _authorizationService = authorizationService;
        _applicationCacheService = applicationCacheService;
        _userService = userService;
        _featureService = featureService;
        _organizationUserRepository = organizationUserRepository;
        _collectionRepository = collectionRepository;
    }

    [HttpGet("{id}")]
    public async Task<GroupResponseModel> Get(string orgId, string id)
    {
        var group = await _groupRepository.GetByIdAsync(new Guid(id));
        if (group == null || !await _currentContext.ManageGroups(group.OrganizationId))
        {
            throw new NotFoundException();
        }

        return new GroupResponseModel(group);
    }

    [HttpGet("{id}/details")]
    public async Task<GroupDetailsResponseModel> GetDetails(string orgId, string id)
    {
        var groupDetails = await _groupRepository.GetByIdWithCollectionsAsync(new Guid(id));
        if (groupDetails?.Item1 == null || !await _currentContext.ManageGroups(groupDetails.Item1.OrganizationId))
        {
            throw new NotFoundException();
        }

        return new GroupDetailsResponseModel(groupDetails.Item1, groupDetails.Item2);
    }

    [HttpGet("")]
    public async Task<ListResponseModel<GroupDetailsResponseModel>> GetOrganizationGroups(Guid orgId)
    {
        var authResult = await _authorizationService.AuthorizeAsync(User, new OrganizationScope(orgId), GroupOperations.ReadAll);
        if (!authResult.Succeeded)
        {
            throw new NotFoundException();
        }

        if (_featureService.IsEnabled(FeatureFlagKeys.SecureOrgGroupDetails))
        {
            var groups = await _groupRepository.GetManyByOrganizationIdAsync(orgId);
            var responses = groups.Select(g => new GroupDetailsResponseModel(g, []));
            return new ListResponseModel<GroupDetailsResponseModel>(responses);
        }

        var groupDetails = await _groupRepository.GetManyWithCollectionsByOrganizationIdAsync(orgId);
        var detailResponses = groupDetails.Select(g => new GroupDetailsResponseModel(g.Item1, g.Item2));
        return new ListResponseModel<GroupDetailsResponseModel>(detailResponses);
    }

    [HttpGet("details")]
    public async Task<ListResponseModel<GroupDetailsResponseModel>> GetOrganizationGroupDetails(Guid orgId)
    {
        var authResult = _featureService.IsEnabled(FeatureFlagKeys.SecureOrgGroupDetails)
            ? await _authorizationService.AuthorizeAsync(User, new OrganizationScope(orgId), GroupOperations.ReadAllDetails)
            : await _authorizationService.AuthorizeAsync(User, new OrganizationScope(orgId), GroupOperations.ReadAll);

        if (!authResult.Succeeded)
        {
            throw new NotFoundException();
        }

        var groups = await _groupRepository.GetManyWithCollectionsByOrganizationIdAsync(orgId);
        var responses = groups.Select(g => new GroupDetailsResponseModel(g.Item1, g.Item2));
        return new ListResponseModel<GroupDetailsResponseModel>(responses);
    }

    [HttpGet("{id}/users")]
    public async Task<IEnumerable<Guid>> GetUsers(string orgId, string id)
    {
        var idGuid = new Guid(id);
        var group = await _groupRepository.GetByIdAsync(idGuid);
        if (group == null || !await _currentContext.ManageGroups(group.OrganizationId))
        {
            throw new NotFoundException();
        }

        var groupIds = await _groupRepository.GetManyUserIdsByIdAsync(idGuid);
        return groupIds;
    }

    [HttpPost("")]
    public async Task<GroupResponseModel> Post(Guid orgId, [FromBody] GroupRequestModel model)
    {
        if (!await _currentContext.ManageGroups(orgId))
        {
            throw new NotFoundException();
        }

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
    [HttpPost("{id}")]
    public async Task<GroupResponseModel> Put(Guid orgId, Guid id, [FromBody] GroupRequestModel model)
    {
        if (!await _currentContext.ManageGroups(orgId))
        {
            throw new NotFoundException();
        }

        var (group, currentAccess) = await _groupRepository.GetByIdWithCollectionsAsync(id);
        if (group == null || group.OrganizationId != orgId)
        {
            throw new NotFoundException();
        }

        // Authorization check:
        // If admins are not allowed access to all collections, you cannot add yourself to a group.
        // No error is thrown for this, we just don't update groups.
        var orgAbility = await _applicationCacheService.GetOrganizationAbilityAsync(orgId);
        if (!orgAbility.AllowAdminAccessToAllCollectionItems)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var organizationUser = await _organizationUserRepository.GetByOrganizationAsync(orgId, userId);
            var currentGroupUsers = await _groupRepository.GetManyUserIdsByIdAsync(id);
            // OrganizationUser may be null if the current user is a provider
            if (organizationUser != null && !currentGroupUsers.Contains(organizationUser.Id) && model.Users.Contains(organizationUser.Id))
            {
                throw new BadRequestException("You cannot add yourself to groups.");
            }
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

    [HttpDelete("{id}")]
    [HttpPost("{id}/delete")]
    public async Task Delete(string orgId, string id)
    {
        var group = await _groupRepository.GetByIdAsync(new Guid(id));
        if (group == null || !await _currentContext.ManageGroups(group.OrganizationId))
        {
            throw new NotFoundException();
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
            if (!await _currentContext.ManageGroups(group.OrganizationId))
            {
                throw new NotFoundException();
            }
        }

        await _deleteGroupCommand.DeleteManyAsync(groups);
    }

    [HttpDelete("{id}/user/{orgUserId}")]
    [HttpPost("{id}/delete-user/{orgUserId}")]
    public async Task Delete(string orgId, string id, string orgUserId)
    {
        var group = await _groupRepository.GetByIdAsync(new Guid(id));
        if (group == null || !await _currentContext.ManageGroups(group.OrganizationId))
        {
            throw new NotFoundException();
        }

        await _groupService.DeleteUserAsync(group, new Guid(orgUserId));
    }
}
