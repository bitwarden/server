using Bit.Api.Models.Request;
using Bit.Api.Models.Response;
using Bit.Api.Utilities;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationCollections.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[Route("organizations/{orgId}/collections")]
[Authorize("Application")]
public class CollectionsController : Controller
{
    private readonly ICollectionRepository _collectionRepository;
    private readonly ICollectionService _collectionService;
    private readonly IDeleteCollectionCommand _deleteCollectionCommand;
    private readonly IUserService _userService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICurrentContext _currentContext;
    private readonly IBulkAddCollectionAccessCommand _bulkAddCollectionAccessCommand;
    private readonly IFeatureService _featureService;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public CollectionsController(
        ICollectionRepository collectionRepository,
        ICollectionService collectionService,
        IDeleteCollectionCommand deleteCollectionCommand,
        IUserService userService,
        IAuthorizationService authorizationService,
        ICurrentContext currentContext,
        IBulkAddCollectionAccessCommand bulkAddCollectionAccessCommand,
        IFeatureService featureService,
        IOrganizationUserRepository organizationUserRepository)
    {
        _collectionRepository = collectionRepository;
        _organizationUserRepository = organizationUserRepository;
        _collectionService = collectionService;
        _deleteCollectionCommand = deleteCollectionCommand;
        _userService = userService;
        _authorizationService = authorizationService;
        _currentContext = currentContext;
        _bulkAddCollectionAccessCommand = bulkAddCollectionAccessCommand;
        _featureService = featureService;
        _organizationUserRepository = organizationUserRepository;
    }

    private bool FlexibleCollectionsIsEnabled => _featureService.IsEnabled(FeatureFlagKeys.FlexibleCollections, _currentContext);

    [HttpGet("{id}")]
    public async Task<CollectionResponseModel> Get(Guid orgId, Guid id)
    {
        if (FlexibleCollectionsIsEnabled)
        {
            // New flexible collections logic
            return await Get_vNext(id);
        }

        // Old pre-flexible collections logic follows
        if (!await CanViewCollectionAsync(orgId, id))
        {
            throw new NotFoundException();
        }

        var collection = await GetCollectionAsync(id, orgId);

        return new CollectionResponseModel(collection);
    }

    [HttpGet("{id}/details")]
    public async Task<CollectionAccessDetailsResponseModel> GetDetails(Guid orgId, Guid id)
    {
        if (FlexibleCollectionsIsEnabled)
        {
            // New flexible collections logic
            return await GetDetails_vNext(id);
        }

        // Old pre-flexible collections logic follows
        if (!await ViewAtLeastOneCollectionAsync(orgId) && !await _currentContext.ManageUsers(orgId))
        {
            throw new NotFoundException();
        }

        if (await _currentContext.ViewAllCollections(orgId))
        {
            (var collection, var access) = await _collectionRepository.GetByIdWithAccessAsync(id);
            if (collection == null || collection.OrganizationId != orgId)
            {
                throw new NotFoundException();
            }

            return new CollectionAccessDetailsResponseModel(collection, access.Groups, access.Users);
        }
        else
        {
            (var collection, var access) = await _collectionRepository.GetByIdWithAccessAsync(id,
                _currentContext.UserId.Value, FlexibleCollectionsIsEnabled);
            if (collection == null || collection.OrganizationId != orgId)
            {
                throw new NotFoundException();
            }

            return new CollectionAccessDetailsResponseModel(collection, access.Groups, access.Users);
        }
    }

    [HttpGet("details")]
    public async Task<ListResponseModel<CollectionAccessDetailsResponseModel>> GetManyWithDetails(Guid orgId)
    {
        if (FlexibleCollectionsIsEnabled)
        {
            // New flexible collections logic
            return await GetManyWithDetails_vNext(orgId);
        }

        // Old pre-flexible collections logic follows
        if (!await ViewAtLeastOneCollectionAsync(orgId) && !await _currentContext.ManageUsers(orgId) && !await _currentContext.ManageGroups(orgId))
        {
            throw new NotFoundException();
        }

        // We always need to know which collections the current user is assigned to
        var assignedOrgCollections =
            await _collectionRepository.GetManyByUserIdWithAccessAsync(_currentContext.UserId.Value, orgId,
                FlexibleCollectionsIsEnabled);

        if (await _currentContext.ViewAllCollections(orgId) || await _currentContext.ManageUsers(orgId))
        {
            // The user can view all collections, but they may not always be assigned to all of them
            var allOrgCollections = await _collectionRepository.GetManyByOrganizationIdWithAccessAsync(orgId);

            return new ListResponseModel<CollectionAccessDetailsResponseModel>(allOrgCollections.Select(c =>
                new CollectionAccessDetailsResponseModel(c.Item1, c.Item2.Groups, c.Item2.Users)
                {
                    // Manually determine which collections they're assigned to
                    Assigned = assignedOrgCollections.Any(ac => ac.Item1.Id == c.Item1.Id)
                })
            );
        }

        return new ListResponseModel<CollectionAccessDetailsResponseModel>(assignedOrgCollections.Select(c =>
            new CollectionAccessDetailsResponseModel(c.Item1, c.Item2.Groups, c.Item2.Users)
            {
                Assigned = true // Mapping from assignedOrgCollections implies they're all assigned
            })
        );
    }

    [HttpGet("")]
    public async Task<ListResponseModel<CollectionResponseModel>> Get(Guid orgId)
    {
        if (FlexibleCollectionsIsEnabled)
        {
            // New flexible collections logic
            return await GetByOrgId_vNext(orgId);
        }

        // Old pre-flexible collections logic follows
        IEnumerable<Collection> orgCollections = null;
        if (await _currentContext.ManageGroups(orgId))
        {
            // ManageGroups users need to see all collections to manage other users' collection access.
            // This is not added to collectionService.GetOrganizationCollectionsAsync as that may have
            // unintended consequences on other logic that also uses that method.
            // This is a quick fix but it will be properly fixed by permission changes in Flexible Collections.

            // Get all collections for organization
            orgCollections = await _collectionRepository.GetManyByOrganizationIdAsync(orgId);
        }
        else
        {
            // Returns all collections or collections the user is assigned to, depending on permissions
            orgCollections = await _collectionService.GetOrganizationCollectionsAsync(orgId);
        }

        var responses = orgCollections.Select(c => new CollectionResponseModel(c));
        return new ListResponseModel<CollectionResponseModel>(responses);
    }

    [HttpGet("managed")]
    public async Task<ListResponseModel<CollectionResponseModel>> GetManaged(Guid orgId)
    {
        IEnumerable<Collection> orgCollections = await _collectionService.GetOrganizationManagedCollectionsAsync(orgId);

        var responses = orgCollections.Select(c => new CollectionResponseModel(c));
        return new ListResponseModel<CollectionResponseModel>(responses);
    }

    [HttpGet("~/collections")]
    public async Task<ListResponseModel<CollectionDetailsResponseModel>> GetUser()
    {
        var collections = await _collectionRepository.GetManyByUserIdAsync(
            _userService.GetProperUserId(User).Value, FlexibleCollectionsIsEnabled);
        var responses = collections.Select(c => new CollectionDetailsResponseModel(c));
        return new ListResponseModel<CollectionDetailsResponseModel>(responses);
    }

    [HttpGet("{id}/users")]
    public async Task<IEnumerable<SelectionReadOnlyResponseModel>> GetUsers(Guid orgId, Guid id)
    {
        if (FlexibleCollectionsIsEnabled)
        {
            // New flexible collections logic
            return await GetUsers_vNext(id);
        }

        // Old pre-flexible collections logic follows
        var collection = await GetCollectionAsync(id, orgId);
        var collectionUsers = await _collectionRepository.GetManyUsersByIdAsync(collection.Id);
        var responses = collectionUsers.Select(cu => new SelectionReadOnlyResponseModel(cu));
        return responses;
    }

    [HttpPost("")]
    public async Task<CollectionResponseModel> Post(Guid orgId, [FromBody] CollectionRequestModel model)
    {
        var collection = model.ToCollection(orgId);

        var authorized = FlexibleCollectionsIsEnabled
            ? (await _authorizationService.AuthorizeAsync(User, collection, BulkCollectionOperations.Create)).Succeeded
            : await CanCreateCollection(orgId, collection.Id) || await CanEditCollectionAsync(orgId, collection.Id);
        if (!authorized)
        {
            throw new NotFoundException();
        }

        var groups = model.Groups?.Select(g => g.ToSelectionReadOnly());
        var users = model.Users?.Select(g => g.ToSelectionReadOnly()).ToList() ?? new List<CollectionAccessSelection>();

        // Pre-flexible collections logic assigned Managers to collections they create
        var assignUserToCollection =
            !FlexibleCollectionsIsEnabled &&
            !await _currentContext.EditAnyCollection(orgId) &&
            await _currentContext.EditAssignedCollections(orgId);
        var isNewCollection = collection.Id == default;

        if (assignUserToCollection && isNewCollection && _currentContext.UserId.HasValue)
        {
            var orgUser = await _organizationUserRepository.GetByOrganizationAsync(orgId, _currentContext.UserId.Value);
            // don't add duplicate access if the user has already specified it themselves
            var existingAccess = users.Any(u => u.Id == orgUser.Id);
            if (orgUser is { Status: OrganizationUserStatusType.Confirmed } && !existingAccess)
            {
                users.Add(new CollectionAccessSelection
                {
                    Id = orgUser.Id,
                    ReadOnly = false
                });
            }
        }

        await _collectionService.SaveAsync(collection, groups, users);
        return new CollectionResponseModel(collection);
    }

    [HttpPut("{id}")]
    [HttpPost("{id}")]
    public async Task<CollectionResponseModel> Put(Guid orgId, Guid id, [FromBody] CollectionRequestModel model)
    {
        if (FlexibleCollectionsIsEnabled)
        {
            // New flexible collections logic
            return await Put_vNext(id, model);
        }

        // Old pre-flexible collections logic follows
        if (!await CanEditCollectionAsync(orgId, id))
        {
            throw new NotFoundException();
        }

        var collection = await GetCollectionAsync(id, orgId);
        var groups = model.Groups?.Select(g => g.ToSelectionReadOnly());
        var users = model.Users?.Select(g => g.ToSelectionReadOnly());
        await _collectionService.SaveAsync(model.ToCollection(collection), groups, users);
        return new CollectionResponseModel(collection);
    }

    [HttpPut("{id}/users")]
    public async Task PutUsers(Guid orgId, Guid id, [FromBody] IEnumerable<SelectionReadOnlyRequestModel> model)
    {
        if (FlexibleCollectionsIsEnabled)
        {
            // New flexible collections logic
            await PutUsers_vNext(id, model);
            return;
        }

        // Old pre-flexible collections logic follows
        if (!await CanEditCollectionAsync(orgId, id))
        {
            throw new NotFoundException();
        }

        var collection = await GetCollectionAsync(id, orgId);
        await _collectionRepository.UpdateUsersAsync(collection.Id, model?.Select(g => g.ToSelectionReadOnly()));
    }

    [HttpPost("bulk-access")]
    [RequireFeature(FeatureFlagKeys.BulkCollectionAccess)]
    // Also gated behind Flexible Collections flag because it only has new authorization logic.
    // Could be removed if legacy authorization logic were implemented for many collections.
    [RequireFeature(FeatureFlagKeys.FlexibleCollections)]
    public async Task PostBulkCollectionAccess([FromBody] BulkCollectionAccessRequestModel model)
    {
        var collections = await _collectionRepository.GetManyByManyIdsAsync(model.CollectionIds);

        if (collections.Count != model.CollectionIds.Count())
        {
            throw new NotFoundException("One or more collections not found.");
        }

        var result = await _authorizationService.AuthorizeAsync(User, collections, BulkCollectionOperations.ModifyAccess);

        if (!result.Succeeded)
        {
            throw new NotFoundException();
        }

        await _bulkAddCollectionAccessCommand.AddAccessAsync(
            collections,
            model.Users?.Select(u => u.ToSelectionReadOnly()).ToList(),
            model.Groups?.Select(g => g.ToSelectionReadOnly()).ToList());
    }

    [HttpDelete("{id}")]
    [HttpPost("{id}/delete")]
    public async Task Delete(Guid orgId, Guid id)
    {
        if (FlexibleCollectionsIsEnabled)
        {
            // New flexible collections logic
            await Delete_vNext(id);
            return;
        }

        // Old pre-flexible collections logic follows
        if (!await CanDeleteCollectionAsync(orgId, id))
        {
            throw new NotFoundException();
        }

        var collection = await GetCollectionAsync(id, orgId);
        await _deleteCollectionCommand.DeleteAsync(collection);
    }

    [HttpDelete("")]
    [HttpPost("delete")]
    public async Task DeleteMany(Guid orgId, [FromBody] CollectionBulkDeleteRequestModel model)
    {
        if (FlexibleCollectionsIsEnabled)
        {
            // New flexible collections logic
            var collections = await _collectionRepository.GetManyByManyIdsAsync(model.Ids);
            var result = await _authorizationService.AuthorizeAsync(User, collections, BulkCollectionOperations.Delete);
            if (!result.Succeeded)
            {
                throw new NotFoundException();
            }

            await _deleteCollectionCommand.DeleteManyAsync(collections);
            return;
        }

        // Old pre-flexible collections logic follows
        if (!await _currentContext.DeleteAssignedCollections(orgId) && !await DeleteAnyCollection(orgId))
        {
            throw new NotFoundException();
        }

        var userCollections = await _collectionService.GetOrganizationCollectionsAsync(orgId);
        var filteredCollections = userCollections
            .Where(c => model.Ids.Contains(c.Id) && c.OrganizationId == orgId);

        if (!filteredCollections.Any())
        {
            throw new BadRequestException("No collections found.");
        }

        await _deleteCollectionCommand.DeleteManyAsync(filteredCollections);
    }

    [HttpDelete("{id}/user/{orgUserId}")]
    [HttpPost("{id}/delete-user/{orgUserId}")]
    public async Task DeleteUser(Guid orgId, Guid id, Guid orgUserId)
    {
        if (FlexibleCollectionsIsEnabled)
        {
            // New flexible collections logic
            await DeleteUser_vNext(id, orgUserId);
            return;
        }

        // Old pre-flexible collections logic follows
        var collection = await GetCollectionAsync(id, orgId);
        await _collectionService.DeleteUserAsync(collection, orgUserId);
    }

    private void DeprecatedPermissionsGuard()
    {
        if (FlexibleCollectionsIsEnabled)
        {
            throw new FeatureUnavailableException("Flexible Collections is ON when it should be OFF.");
        }
    }

    [Obsolete("Pre-Flexible Collections logic. Will be replaced by CollectionsAuthorizationHandler.")]
    private async Task<Collection> GetCollectionAsync(Guid id, Guid orgId)
    {
        DeprecatedPermissionsGuard();

        Collection collection = default;
        if (await _currentContext.ViewAllCollections(orgId))
        {
            collection = await _collectionRepository.GetByIdAsync(id);
        }
        else if (await _currentContext.ViewAssignedCollections(orgId))
        {
            collection = await _collectionRepository.GetByIdAsync(id, _currentContext.UserId.Value, FlexibleCollectionsIsEnabled);
        }

        if (collection == null || collection.OrganizationId != orgId)
        {
            throw new NotFoundException();
        }

        return collection;
    }

    [Obsolete("Pre-Flexible Collections logic. Will be replaced by CollectionsAuthorizationHandler.")]
    private async Task<bool> CanCreateCollection(Guid orgId, Guid collectionId)
    {
        DeprecatedPermissionsGuard();

        if (collectionId != default)
        {
            return false;
        }

        return await _currentContext.OrganizationManager(orgId) || (_currentContext.Organizations?.Any(o => o.Id == orgId &&
            (o.Permissions?.CreateNewCollections ?? false)) ?? false);
    }

    [Obsolete("Pre-Flexible Collections logic. Will be replaced by CollectionsAuthorizationHandler.")]
    private async Task<bool> CanEditCollectionAsync(Guid orgId, Guid collectionId)
    {
        DeprecatedPermissionsGuard();

        if (collectionId == default)
        {
            return false;
        }

        if (await _currentContext.EditAnyCollection(orgId))
        {
            return true;
        }

        if (await _currentContext.EditAssignedCollections(orgId))
        {
            var collectionDetails =
                await _collectionRepository.GetByIdAsync(collectionId, _currentContext.UserId.Value, FlexibleCollectionsIsEnabled);
            return collectionDetails != null;
        }

        return false;
    }

    [Obsolete("Pre-Flexible Collections logic. Will be replaced by CollectionsAuthorizationHandler.")]
    private async Task<bool> CanDeleteCollectionAsync(Guid orgId, Guid collectionId)
    {
        DeprecatedPermissionsGuard();

        if (collectionId == default)
        {
            return false;
        }

        if (await DeleteAnyCollection(orgId))
        {
            return true;
        }

        if (await _currentContext.DeleteAssignedCollections(orgId))
        {
            var collectionDetails =
                await _collectionRepository.GetByIdAsync(collectionId, _currentContext.UserId.Value, FlexibleCollectionsIsEnabled);
            return collectionDetails != null;
        }

        return false;
    }

    [Obsolete("Pre-Flexible Collections logic. Will be replaced by CollectionsAuthorizationHandler.")]
    private async Task<bool> DeleteAnyCollection(Guid orgId)
    {
        DeprecatedPermissionsGuard();

        return await _currentContext.OrganizationAdmin(orgId) ||
            (_currentContext.Organizations?.Any(o => o.Id == orgId
                && (o.Permissions?.DeleteAnyCollection ?? false)) ?? false);
    }

    [Obsolete("Pre-Flexible Collections logic. Will be replaced by CollectionsAuthorizationHandler.")]
    private async Task<bool> CanViewCollectionAsync(Guid orgId, Guid collectionId)
    {
        DeprecatedPermissionsGuard();

        if (collectionId == default)
        {
            return false;
        }

        if (await _currentContext.ViewAllCollections(orgId))
        {
            return true;
        }

        if (await _currentContext.ViewAssignedCollections(orgId))
        {
            var collectionDetails =
                await _collectionRepository.GetByIdAsync(collectionId, _currentContext.UserId.Value, FlexibleCollectionsIsEnabled);
            return collectionDetails != null;
        }

        return false;
    }

    [Obsolete("Pre-Flexible Collections logic. Will be replaced by CollectionsAuthorizationHandler.")]
    private async Task<bool> ViewAtLeastOneCollectionAsync(Guid orgId)
    {
        DeprecatedPermissionsGuard();

        return await _currentContext.ViewAllCollections(orgId) || await _currentContext.ViewAssignedCollections(orgId);
    }

    private async Task<CollectionResponseModel> Get_vNext(Guid collectionId)
    {
        var collection = await _collectionRepository.GetByIdAsync(collectionId);
        var authorized = (await _authorizationService.AuthorizeAsync(User, collection, BulkCollectionOperations.Read)).Succeeded;
        if (!authorized)
        {
            throw new NotFoundException();
        }

        return new CollectionResponseModel(collection);
    }

    private async Task<CollectionAccessDetailsResponseModel> GetDetails_vNext(Guid id)
    {
        // New flexible collections logic
        var (collection, access) = await _collectionRepository.GetByIdWithAccessAsync(id);
        var authorized = (await _authorizationService.AuthorizeAsync(User, collection, BulkCollectionOperations.ReadWithAccess)).Succeeded;
        if (!authorized)
        {
            throw new NotFoundException();
        }

        return new CollectionAccessDetailsResponseModel(collection, access.Groups, access.Users);
    }

    private async Task<ListResponseModel<CollectionAccessDetailsResponseModel>> GetManyWithDetails_vNext(Guid orgId)
    {
        // We always need to know which collections the current user is assigned to
        var assignedOrgCollections = await _collectionRepository
            .GetManyByUserIdWithAccessAsync(_currentContext.UserId.Value, orgId, FlexibleCollectionsIsEnabled);

        var readAllAuthorized =
            (await _authorizationService.AuthorizeAsync(User, CollectionOperations.ReadAllWithAccess(orgId))).Succeeded;
        if (readAllAuthorized)
        {
            // The user can view all collections, but they may not always be assigned to all of them
            var allOrgCollections = await _collectionRepository.GetManyByOrganizationIdWithAccessAsync(orgId);

            return new ListResponseModel<CollectionAccessDetailsResponseModel>(allOrgCollections.Select(c =>
                new CollectionAccessDetailsResponseModel(c.Item1, c.Item2.Groups, c.Item2.Users)
                {
                    // Manually determine which collections they're assigned to
                    Assigned = assignedOrgCollections.Any(ac => ac.Item1.Id == c.Item1.Id)
                })
            );
        }

        // Filter the assigned collections to only return those where the user has Manage permission
        var manageableOrgCollections = assignedOrgCollections.Where(c => c.Item1.Manage).ToList();
        var readAssignedAuthorized = await _authorizationService.AuthorizeAsync(User, manageableOrgCollections.Select(c => c.Item1), BulkCollectionOperations.ReadWithAccess);
        if (!readAssignedAuthorized.Succeeded)
        {
            throw new NotFoundException();
        }

        return new ListResponseModel<CollectionAccessDetailsResponseModel>(manageableOrgCollections.Select(c =>
            new CollectionAccessDetailsResponseModel(c.Item1, c.Item2.Groups, c.Item2.Users)
            {
                Assigned = true // Mapping from manageableOrgCollections implies they're all assigned
            })
        );
    }

    private async Task<ListResponseModel<CollectionResponseModel>> GetByOrgId_vNext(Guid orgId)
    {
        IEnumerable<Collection> orgCollections;

        var readAllAuthorized = (await _authorizationService.AuthorizeAsync(User, CollectionOperations.ReadAll(orgId))).Succeeded;
        if (readAllAuthorized)
        {
            orgCollections = await _collectionRepository.GetManyByOrganizationIdAsync(orgId);
        }
        else
        {
            var collections = await _collectionRepository.GetManyByUserIdAsync(_currentContext.UserId.Value, FlexibleCollectionsIsEnabled);
            var readAuthorized = (await _authorizationService.AuthorizeAsync(User, collections, BulkCollectionOperations.Read)).Succeeded;
            if (readAuthorized)
            {
                orgCollections = collections.Where(c => c.OrganizationId == orgId);
            }
            else
            {
                throw new NotFoundException();
            }
        }

        var responses = orgCollections.Select(c => new CollectionResponseModel(c));
        return new ListResponseModel<CollectionResponseModel>(responses);
    }

    private async Task<IEnumerable<SelectionReadOnlyResponseModel>> GetUsers_vNext(Guid id)
    {
        var collection = await _collectionRepository.GetByIdAsync(id);
        var authorized = (await _authorizationService.AuthorizeAsync(User, collection, BulkCollectionOperations.ReadAccess)).Succeeded;
        if (!authorized)
        {
            throw new NotFoundException();
        }

        var collectionUsers = await _collectionRepository.GetManyUsersByIdAsync(collection.Id);
        var responses = collectionUsers.Select(cu => new SelectionReadOnlyResponseModel(cu));
        return responses;
    }

    private async Task<CollectionResponseModel> Put_vNext(Guid id, CollectionRequestModel model)
    {
        var collection = await _collectionRepository.GetByIdAsync(id);
        var authorized = (await _authorizationService.AuthorizeAsync(User, collection, BulkCollectionOperations.Update)).Succeeded;
        if (!authorized)
        {
            throw new NotFoundException();
        }

        var groups = model.Groups?.Select(g => g.ToSelectionReadOnly());
        var users = model.Users?.Select(g => g.ToSelectionReadOnly());
        await _collectionService.SaveAsync(model.ToCollection(collection), groups, users);
        return new CollectionResponseModel(collection);
    }

    private async Task PutUsers_vNext(Guid id, IEnumerable<SelectionReadOnlyRequestModel> model)
    {
        var collection = await _collectionRepository.GetByIdAsync(id);
        var authorized = (await _authorizationService.AuthorizeAsync(User, collection, BulkCollectionOperations.ModifyAccess)).Succeeded;
        if (!authorized)
        {
            throw new NotFoundException();
        }

        await _collectionRepository.UpdateUsersAsync(collection.Id, model?.Select(g => g.ToSelectionReadOnly()));
    }

    private async Task Delete_vNext(Guid id)
    {
        var collection = await _collectionRepository.GetByIdAsync(id);
        var authorized = (await _authorizationService.AuthorizeAsync(User, collection, BulkCollectionOperations.Delete)).Succeeded;
        if (!authorized)
        {
            throw new NotFoundException();
        }

        await _deleteCollectionCommand.DeleteAsync(collection);
    }

    private async Task DeleteUser_vNext(Guid id, Guid orgUserId)
    {
        var collection = await _collectionRepository.GetByIdAsync(id);
        var authorized = (await _authorizationService.AuthorizeAsync(User, collection, BulkCollectionOperations.ModifyAccess)).Succeeded;
        if (!authorized)
        {
            throw new NotFoundException();
        }

        await _collectionService.DeleteUserAsync(collection, orgUserId);
    }
}
