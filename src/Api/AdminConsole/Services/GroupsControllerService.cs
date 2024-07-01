using System.Security.Claims;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Api.AdminConsole.Models.Response;
using Bit.Api.Utilities;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Api.Vault.AuthorizationHandlers.Groups;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace Api.AdminConsole.Services;

public class GroupsControllerService : IGroupsControllerService
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

    public GroupsControllerService(
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

    /// <summary>
    /// Gets the basic group information of an organization
    /// </summary>
    /// <param name="orgId">Organization id/param>
    /// <param name="groupId">Group id</param>
    /// <returns>GroupResponseModel</returns>
    public async Task<GroupResponseModel> GetOrganizationGroup(string orgId, string groupId)
    {
        var group = await _groupRepository.GetByIdAsync(new Guid(groupId));
        if (group == null || !await _currentContext.ManageGroups(group.OrganizationId))
        {
            throw new NotFoundException();
        }

        return new GroupResponseModel(group);
    }

    /// <summary>
    /// Gets the detailed group information of an organization
    /// </summary>
    /// <param name="orgId">Organization id</param>
    /// <param name="groupId">Group id</param>
    /// <returns>GroupDetailsResponseModel</returns>
    public async Task<GroupDetailsResponseModel> GetOrganizationGroupDetail(string orgId, string groupId)
    {
        var groupDetails = await _groupRepository.GetByIdWithCollectionsAsync(new Guid(groupId));
        if (groupDetails?.Item1 == null || !await _currentContext.ManageGroups(groupDetails.Item1.OrganizationId))
        {
            throw new NotFoundException();
        }

        return new GroupDetailsResponseModel(groupDetails.Item1, groupDetails.Item2);
    }

    /// <summary>
    /// Gets the detailed information on all groups in an organization
    /// </summary>
    /// <param name="user">
    ///     Requesting user. This user must have the ability to 
    ///     view all groups in the organization</param>
    /// <param name="orgId">Organization id</param>
    /// <returns>List of GroupDetailsResponseModel</returns>
    public async Task<IEnumerable<GroupDetailsResponseModel>> GetOrganizationGroupsDetails(ClaimsPrincipal user, Guid orgId)
    {
        var authorized =
            (await _authorizationService.AuthorizeAsync(user, GroupOperations.ReadAll(orgId))).Succeeded;
        if (!authorized)
        {
            throw new NotFoundException();
        }

        var groups = await _groupRepository.GetManyWithCollectionsByOrganizationIdAsync(orgId);
        var responses = groups.Select(g => new GroupDetailsResponseModel(g.Item1, g.Item2));
        return responses;
    }

    /// <summary>
    /// Gets a list of ids for all users in an organization
    /// </summary>
    /// <param name="orgId">Organization id</param>
    /// <returns>List of user Guids</returns>
    public async Task<IEnumerable<Guid>> GetOrganizationUsers(string orgId)
    {
        var idGuid = new Guid(orgId);
        var group = await _groupRepository.GetByIdAsync(idGuid);
        if (group == null || !await _currentContext.ManageGroups(group.OrganizationId))
        {
            throw new NotFoundException();
        }

        var groupIds = await _groupRepository.GetManyUserIdsByIdAsync(idGuid);
        return groupIds;
    }

    /// <summary>
    /// Create a new group in an organization
    /// </summary>
    /// <param name="user">
    ///     Requesting user. The user must have permission to grant access
    ///     for the new group
    /// </param>
    /// <param name="orgId">Organization id</param>
    /// <param name="model">The details for the new group</param>
    /// <returns>GroupResponseModel</returns>
    public async Task<GroupResponseModel> CreateGroup(ClaimsPrincipal user, Guid orgId, GroupRequestModel model)
    {
        if (!await _currentContext.ManageGroups(orgId))
        {
            throw new NotFoundException();
        }

        // Flexible Collections - check the user has permission to grant access to the collections for the new group
        if (_featureService.IsEnabled(Bit.Core.FeatureFlagKeys.FlexibleCollectionsV1) && model.Collections?.Any() == true)
        {
            var collections = await _collectionRepository.GetManyByManyIdsAsync(model.Collections.Select(a => a.Id));
            var authorized =
                (await _authorizationService.AuthorizeAsync(user, collections, BulkCollectionOperations.ModifyGroupAccess))
                .Succeeded;
            if (!authorized)
            {
                throw new NotFoundException("You are not authorized to grant access to these collections.");
            }
        }

        var organization = await _organizationRepository.GetByIdAsync(orgId);
        var group = model.ToGroup(orgId);
        await _createGroupCommand.CreateGroupAsync(group, organization, model.Collections?.Select(c => c.ToSelectionReadOnly()).ToList(), model.Users);

        return new GroupResponseModel(group);
    }

    /// <summary>
    /// Updates a group in an organization
    /// </summary>
    /// <param name="user">
    ///     The requesting user. The requesting user must have the proper
    ///     permissions for editing items within the group.
    /// </param>
    /// <param name="orgId">Organization id</param>
    /// <param name="groupId"></param>
    /// <param name="model"></param>
    /// <returns>Updated GroupResponseModel</returns>
    public async Task<GroupResponseModel> UpdateGroup(ClaimsPrincipal user, Guid orgId, Guid groupId, GroupRequestModel model)
    {
        if (_featureService.IsEnabled(Bit.Core.FeatureFlagKeys.FlexibleCollectionsV1))
        {
            // Use new Flexible Collections v1 logic
            return await Put_vNext(user, orgId, groupId, model);
        }

        // Pre-Flexible Collections v1 logic follows
        var group = await _groupRepository.GetByIdAsync(groupId);
        if (group == null || !await _currentContext.ManageGroups(group.OrganizationId))
        {
            throw new NotFoundException();
        }

        var organization = await _organizationRepository.GetByIdAsync(orgId);

        await _updateGroupCommand.UpdateGroupAsync(model.ToGroup(group), organization,
            model.Collections.Select(c => c.ToSelectionReadOnly()).ToList(), model.Users);
        return new GroupResponseModel(group);
    }

    /// <summary>
    /// Delete a group from an organization
    /// </summary>
    /// <param name="orgId">Organization id</param>
    /// <param name="groupId">Group id</param>
    public async Task DeleteGroup(string orgId, string groupId)
    {
        var group = await _groupRepository.GetByIdAsync(new Guid(groupId));
        if (group == null || !await _currentContext.ManageGroups(group.OrganizationId))
        {
            throw new NotFoundException();
        }

        await _deleteGroupCommand.DeleteAsync(group);
    }

    /// <summary>
    /// Deletes multiple groups from an organization
    /// </summary>
    /// <param name="model">The details for what groups to remove</param>
    public async Task BulkDeleteGroups(GroupBulkRequestModel model)
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

    /// <summary>
    /// Deletes a user from a group in an organization
    /// </summary>
    /// <param name="orgId">Organization id</param>
    /// <param name="groupId">Group id</param>
    /// <param name="orgUserId">User id for the user to delete</param>
    public async Task DeleteGroupUser(string orgId, string groupId, string orgUserId)
    {
        var group = await _groupRepository.GetByIdAsync(new Guid(groupId));
        if (group == null || !await _currentContext.ManageGroups(group.OrganizationId))
        {
            throw new NotFoundException();
        }

        await _groupService.DeleteUserAsync(group, new Guid(orgUserId));
    }

    /// <summary>
    /// Put logic for Flexible Collections v1
    /// </summary>
    private async Task<GroupResponseModel> Put_vNext(ClaimsPrincipal user, Guid orgId, Guid id, GroupRequestModel model)
    {
        var (group, currentAccess) = await _groupRepository.GetByIdWithCollectionsAsync(id);
        if (group == null || !await _currentContext.ManageGroups(group.OrganizationId))
        {
            throw new NotFoundException();
        }

        // Check whether the user is permitted to add themselves to the group
        var orgAbility = await _applicationCacheService.GetOrganizationAbilityAsync(orgId);
        if (!orgAbility.AllowAdminAccessToAllCollectionItems)
        {
            var userId = _userService.GetProperUserId(user).Value;
            var organizationUser = await _organizationUserRepository.GetByOrganizationAsync(orgId, userId);
            var currentGroupUsers = await _groupRepository.GetManyUserIdsByIdAsync(id);
            // OrganizationUser may be null if the current user is a provider
            if (organizationUser != null && !currentGroupUsers.Contains(organizationUser.Id) && model.Users.Contains(organizationUser.Id))
            {
                throw new BadRequestException("You cannot add yourself to groups.");
            }
        }

        // The client only sends collections that the saving user has permissions to edit.
        // On the server side, we need to (1) confirm this and (2) concat these with the collections that the user
        // can't edit before saving to the database.
        var currentCollections = await _collectionRepository
            .GetManyByManyIdsAsync(currentAccess.Select(cas => cas.Id));

        var readonlyCollectionIds = new HashSet<Guid>();
        foreach (var collection in currentCollections)
        {
            if (!(await _authorizationService.AuthorizeAsync(user, collection, BulkCollectionOperations.ModifyGroupAccess))
                .Succeeded)
            {
                readonlyCollectionIds.Add(collection.Id);
            }
        }

        if (model.Collections.Any(c => readonlyCollectionIds.Contains(c.Id)))
        {
            throw new BadRequestException("You must have Can Manage permissions to edit a collection's membership");
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
}
