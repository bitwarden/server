// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.AdminConsole.Authorization.Collections;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Api.AdminConsole.Models.Response;
using Bit.Api.Models.Response;
using Bit.Core;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.OrganizationFeatures.Collections.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyGroupAccess;
using Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyUserAccess;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

[Route("organizations/{orgId}/collections")]
[Authorize("Application")]
public class CollectionsController : BaseAdminConsoleController
{
    private readonly ICollectionRepository _collectionRepository;
    private readonly ICreateCollectionCommand _createCollectionCommand;
    private readonly IUpdateCollectionCommand _updateCollectionCommand;
    private readonly IDeleteCollectionCommand _deleteCollectionCommand;
    private readonly IUserService _userService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICurrentContext _currentContext;
    private readonly IBulkAddCollectionAccessCommand _bulkAddCollectionAccessCommand;
    private readonly IProviderService _providerService;
    private readonly ICollectionAuthorizationService _collectionAuthorizationService;
    private readonly IOrganizationAbilityCacheService _organizationAbilityCacheService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IModifyCollectionUserAccessCommand _modifyCollectionUserAccessCommand;
    private readonly IModifyCollectionGroupAccessCommand _modifyCollectionGroupAccessCommand;

    public CollectionsController(
        ICollectionRepository collectionRepository,
        ICreateCollectionCommand createCollectionCommand,
        IUpdateCollectionCommand updateCollectionCommand,
        IDeleteCollectionCommand deleteCollectionCommand,
        IUserService userService,
        IAuthorizationService authorizationService,
        ICurrentContext currentContext,
        IBulkAddCollectionAccessCommand bulkAddCollectionAccessCommand,
        IProviderService providerService,
        ICollectionAuthorizationService collectionAuthorizationService,
        IOrganizationAbilityCacheService organizationAbilityCacheService,
        IOrganizationUserRepository organizationUserRepository,
        IModifyCollectionUserAccessCommand modifyCollectionUserAccessCommand,
        IModifyCollectionGroupAccessCommand modifyCollectionGroupAccessCommand)
    {
        _collectionRepository = collectionRepository;
        _createCollectionCommand = createCollectionCommand;
        _updateCollectionCommand = updateCollectionCommand;
        _deleteCollectionCommand = deleteCollectionCommand;
        _userService = userService;
        _authorizationService = authorizationService;
        _currentContext = currentContext;
        _bulkAddCollectionAccessCommand = bulkAddCollectionAccessCommand;
        _providerService = providerService;
        _collectionAuthorizationService = collectionAuthorizationService;
        _organizationAbilityCacheService = organizationAbilityCacheService;
        _organizationUserRepository = organizationUserRepository;
        _modifyCollectionUserAccessCommand = modifyCollectionUserAccessCommand;
        _modifyCollectionGroupAccessCommand = modifyCollectionGroupAccessCommand;
    }

    [HttpGet("{id}")]
    public async Task<CollectionResponseModel> Get(Guid orgId, Guid id)
    {
        var collection = await _collectionRepository.GetByIdAsync(id);
        var authorized = (await _authorizationService.AuthorizeAsync(User, collection, BulkCollectionOperations.Read)).Succeeded;
        if (!authorized)
        {
            throw new NotFoundException();
        }

        return new CollectionResponseModel(collection);
    }

    [HttpGet("{id}/details")]
    public async Task<CollectionAccessDetailsResponseModel> GetDetails(Guid orgId, Guid id)
    {
        var collectionAdminDetails =
            await _collectionRepository.GetByIdWithPermissionsAsync(id, _currentContext.UserId, true);

        var authorized = (await _authorizationService.AuthorizeAsync(User, collectionAdminDetails, BulkCollectionOperations.ReadWithAccess)).Succeeded;
        if (!authorized)
        {
            throw new NotFoundException();
        }

        return new CollectionAccessDetailsResponseModel(collectionAdminDetails);
    }

    [HttpGet("details")]
    public async Task<ListResponseModel<CollectionAccessDetailsResponseModel>> GetManyWithDetails(Guid orgId)
    {
        var allOrgCollections = await _collectionRepository.GetManySharedByOrganizationIdWithPermissionsAsync(
            orgId, _currentContext.UserId.Value, true);
        if (await _currentContext.ProviderUserForOrgAsync(orgId))
        {
            await _providerService.LogProviderAccessToOrganizationAsync(orgId);
        }

        var readAllAuthorized =
            (await _authorizationService.AuthorizeAsync(User, CollectionOperations.ReadAllWithAccess(orgId))).Succeeded;
        if (readAllAuthorized)
        {
            return new ListResponseModel<CollectionAccessDetailsResponseModel>(
                allOrgCollections.Select(c => new CollectionAccessDetailsResponseModel(c))
            );
        }

        // Filter collections to only return those where the user has Manage permission
        var manageableOrgCollections = allOrgCollections.Where(c => c.Manage).ToList();

        return new ListResponseModel<CollectionAccessDetailsResponseModel>(manageableOrgCollections.Select(c =>
            new CollectionAccessDetailsResponseModel(c)
        ));
    }

    [HttpGet("")]
    public async Task<ListResponseModel<CollectionResponseModel>> GetAll(Guid orgId)
    {
        IEnumerable<Collection> orgCollections;

        var readAllAuthorized = (await _authorizationService.AuthorizeAsync(User, CollectionOperations.ReadAll(orgId))).Succeeded;
        if (readAllAuthorized)
        {
            orgCollections = await _collectionRepository.GetManySharedCollectionsByOrganizationIdAsync(orgId);
        }
        else
        {
            var assignedCollections = await _collectionRepository.GetManyByUserIdAsync(_currentContext.UserId.Value);
            orgCollections = assignedCollections.Where(c => c.OrganizationId == orgId && c.Manage).ToList();
        }

        var responses = orgCollections.Select(c => new CollectionResponseModel(c));
        return new ListResponseModel<CollectionResponseModel>(responses);
    }

    [HttpGet("~/collections")]
    public async Task<ListResponseModel<CollectionDetailsResponseModel>> GetUser()
    {
        var collections = await _collectionRepository.GetManyByUserIdAsync(
            _userService.GetProperUserId(User).Value);
        var responses = collections.Select(c => new CollectionDetailsResponseModel(c));
        return new ListResponseModel<CollectionDetailsResponseModel>(responses);
    }

    [HttpGet("{id}/users")]
    public async Task<IEnumerable<SelectionReadOnlyResponseModel>> GetUsers(Guid orgId, Guid id)
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

    [HttpPost("")]
    public async Task<CollectionResponseModel> Post(Guid orgId, [FromBody] CreateCollectionRequestModel model)
    {
        var collection = model.ToCollection(orgId);

        var authorized = (await _authorizationService.AuthorizeAsync(User, collection, BulkCollectionOperations.Create)).Succeeded;
        if (!authorized)
        {
            throw new NotFoundException();
        }

        var groups = model.Groups?.Select(g => g.ToSelectionReadOnly());
        var users = model.Users?.Select(g => g.ToSelectionReadOnly()).ToList() ?? new List<CollectionAccessSelection>();

        await _createCollectionCommand.CreateAsync(collection, groups, users);

        if (!_currentContext.UserId.HasValue || (_currentContext.GetOrganization(orgId) == null && await _currentContext.ProviderUserForOrgAsync(orgId)))
        {
            return new CollectionAccessDetailsResponseModel(collection);
        }

        // If we have a user, fetch the latest collection permission details including groups/users
        var collectionWithPermissions = await _collectionRepository.GetByIdWithPermissionsAsync(collection.Id, _currentContext.UserId.Value, true);

        var canReadWithAccess = (await _authorizationService.AuthorizeAsync(User, collectionWithPermissions, BulkCollectionOperations.ReadWithAccess)).Succeeded;
        if (!canReadWithAccess)
        {
            return new CollectionAccessDetailsResponseModel(collection);
        }

        return new CollectionAccessDetailsResponseModel(collectionWithPermissions);
    }

    [HttpPut("{id}")]
    public async Task<CollectionResponseModel> Put(Guid orgId, Guid id, [FromBody] UpdateCollectionRequestModel model)
    {
        var collection = await _collectionRepository.GetByIdAsync(id);
        var authorized = (await _authorizationService.AuthorizeAsync(User, collection, BulkCollectionOperations.Update)).Succeeded;
        if (!authorized)
        {
            throw new NotFoundException();
        }

        var groups = model.Groups?.Select(g => g.ToSelectionReadOnly());
        var users = model.Users?.Select(g => g.ToSelectionReadOnly());
        await _updateCollectionCommand.UpdateAsync(model.ToCollection(collection), groups, users);

        if (!_currentContext.UserId.HasValue || (_currentContext.GetOrganization(collection.OrganizationId) == null && await _currentContext.ProviderUserForOrgAsync(collection.OrganizationId)))
        {
            return new CollectionAccessDetailsResponseModel(collection);
        }

        // If we have a user, fetch the latest collection permission details including groups/users
        var collectionWithPermissions = await _collectionRepository.GetByIdWithPermissionsAsync(collection.Id, _currentContext.UserId.Value, true);

        var canReadWithAccess = (await _authorizationService.AuthorizeAsync(User, collectionWithPermissions, BulkCollectionOperations.ReadWithAccess)).Succeeded;
        if (!canReadWithAccess)
        {
            return new CollectionAccessDetailsResponseModel(collection);
        }

        return new CollectionAccessDetailsResponseModel(collectionWithPermissions);
    }

    [HttpPost("{id}")]
    [Obsolete("This endpoint is deprecated. Use PUT /{id} instead.")]
    public async Task<CollectionResponseModel> PostPut(Guid orgId, Guid id, [FromBody] UpdateCollectionRequestModel model)
    {
        return await Put(orgId, id, model);
    }

    /// <summary>
    /// Like <see cref="Put"/>, but takes add/update/remove deltas for access instead of a full replace list.
    /// </summary>
    [HttpPatch("{id}")]
    [Bitwarden.Server.Sdk.Features.RequireFeature(FeatureFlagKeys.PM12473CollectionUserAccessEndpoint)]
    public async Task<IResult> PatchWithDelta(Guid orgId, Guid id, [FromBody] UpdateCollectionWithDeltaRequestModel model)
    {
        var authorizationResult = await _collectionAuthorizationService.AuthorizeUpdateAsync(orgId, id);
        if (!authorizationResult.IsSuccess)
        {
            throw new NotFoundException();
        }

        // Persistence needs its own copy of the collection's current access details for the delta commands below.
        var (collection, accessDetails) = await _collectionRepository.GetByIdWithAccessAsync(id);
        if (collection is null || collection.OrganizationId != orgId)
        {
            throw new NotFoundException();
        }

        var userTargets = new[] { new CollectionUserAccessTarget(collection, accessDetails) };
        var groupTargets = new[] { new CollectionGroupAccessTarget(collection, accessDetails) };

        var organizationAbility = await _organizationAbilityCacheService.GetOrganizationAbilityAsync(orgId);
        var allowAdminAccessToAllCollectionItems =
            organizationAbility is { AllowAdminAccessToAllCollectionItems: true };

        var callerOrganizationUser = _currentContext.UserId.HasValue
            ? await _organizationUserRepository.GetByOrganizationAsync(orgId, _currentContext.UserId.Value)
            : null;

        if (string.IsNullOrEmpty(collection.DefaultUserCollectionEmail) && !string.IsNullOrWhiteSpace(model.Name))
        {
            collection.Name = model.Name;
        }
        collection.ExternalId = model.ExternalId;

        await _updateCollectionCommand.UpdateAsync(collection);

        var userRequest = new ModifyCollectionUserAccessRequest(
            userTargets,
            model.Users.Add.Select(u => u.ToSelectionReadOnly()).ToList(),
            model.Users.Update.Select(u => u.ToSelectionReadOnly()).ToList(),
            model.Users.Remove.ToList(),
            callerOrganizationUser?.Id,
            allowAdminAccessToAllCollectionItems);

        var userResult = await _modifyCollectionUserAccessCommand.ModifyAsync(userRequest);
        if (userResult.IsError)
        {
            return Handle(userResult, _ => TypedResults.NoContent());
        }

        var groupRequest = new ModifyCollectionGroupAccessRequest(
            groupTargets,
            model.Groups.Add.Select(g => g.ToSelectionReadOnly()).ToList(),
            model.Groups.Update.Select(g => g.ToSelectionReadOnly()).ToList(),
            model.Groups.Remove.ToList(),
            callerOrganizationUser?.Id,
            allowAdminAccessToAllCollectionItems);

        var groupResult = await _modifyCollectionGroupAccessCommand.ModifyAsync(groupRequest);
        return Handle(groupResult);
    }

    [HttpPost("bulk-access")]
    public async Task PostBulkCollectionAccess(Guid orgId, [FromBody] BulkCollectionAccessRequestModel model)
    {
        var collections = await _collectionRepository.GetManyByManyIdsAsync(model.CollectionIds);
        if (collections.Count(c => c.OrganizationId == orgId) != model.CollectionIds.Count())
        {
            throw new NotFoundException("One or more collections not found.");
        }

        var result = await _authorizationService.AuthorizeAsync(User, collections,
            new[] { BulkCollectionOperations.ModifyUserAccess, BulkCollectionOperations.ModifyGroupAccess });

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
    public async Task Delete(Guid orgId, Guid id)
    {
        var collection = await _collectionRepository.GetByIdAsync(id);
        var authorized = (await _authorizationService.AuthorizeAsync(User, collection, BulkCollectionOperations.Delete)).Succeeded;
        if (!authorized)
        {
            throw new NotFoundException();
        }

        await _deleteCollectionCommand.DeleteAsync(collection);
    }

    [HttpPost("{id}/delete")]
    [Obsolete("This endpoint is deprecated. Use DELETE /{id} instead.")]
    public async Task PostDelete(Guid orgId, Guid id)
    {
        await Delete(orgId, id);
    }

    [HttpDelete("")]
    public async Task DeleteMany(Guid orgId, [FromBody] CollectionBulkDeleteRequestModel model)
    {
        var collections = await _collectionRepository.GetManyByManyIdsAsync(model.Ids);
        var result = await _authorizationService.AuthorizeAsync(User, collections, BulkCollectionOperations.Delete);
        if (!result.Succeeded)
        {
            throw new NotFoundException();
        }

        await _deleteCollectionCommand.DeleteManyAsync(collections);
    }

    [HttpPost("delete")]
    [Obsolete("This endpoint is deprecated. Use DELETE / instead.")]
    public async Task PostDeleteMany(Guid orgId, [FromBody] CollectionBulkDeleteRequestModel model)
    {
        await DeleteMany(orgId, model);
    }
}
