using Bit.Api.Models.Request;
using Bit.Api.Models.Response;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core.Context;
using Bit.Core.Entities;
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

    public CollectionsController(
        ICollectionRepository collectionRepository,
        ICollectionService collectionService,
        IDeleteCollectionCommand deleteCollectionCommand,
        IUserService userService,
        IAuthorizationService authorizationService,
        ICurrentContext currentContext,
        IBulkAddCollectionAccessCommand bulkAddCollectionAccessCommand)
    {
        _collectionRepository = collectionRepository;
        _collectionService = collectionService;
        _deleteCollectionCommand = deleteCollectionCommand;
        _userService = userService;
        _authorizationService = authorizationService;
        _currentContext = currentContext;
        _bulkAddCollectionAccessCommand = bulkAddCollectionAccessCommand;
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
        var allOrgCollections = await _collectionRepository.GetManyByOrganizationIdWithPermissionsAsync(
            orgId, _currentContext.UserId.Value, true);

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
    public async Task<ListResponseModel<CollectionResponseModel>> Get(Guid orgId)
    {
        IEnumerable<Collection> orgCollections;

        var readAllAuthorized = (await _authorizationService.AuthorizeAsync(User, CollectionOperations.ReadAll(orgId))).Succeeded;
        if (readAllAuthorized)
        {
            orgCollections = await _collectionRepository.GetManyByOrganizationIdAsync(orgId);
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
    public async Task<CollectionResponseModel> Post(Guid orgId, [FromBody] CollectionRequestModel model)
    {
        var collection = model.ToCollection(orgId);

        var authorized = (await _authorizationService.AuthorizeAsync(User, collection, BulkCollectionOperations.Create)).Succeeded;
        if (!authorized)
        {
            throw new NotFoundException();
        }

        var groups = model.Groups?.Select(g => g.ToSelectionReadOnly());
        var users = model.Users?.Select(g => g.ToSelectionReadOnly()).ToList() ?? new List<CollectionAccessSelection>();

        await _collectionService.SaveAsync(collection, groups, users);

        if (!_currentContext.UserId.HasValue || (_currentContext.GetOrganization(orgId) == null && await _currentContext.ProviderUserForOrgAsync(orgId)))
        {
            return new CollectionAccessDetailsResponseModel(collection);
        }

        // If we have a user, fetch the latest collection permission details
        var collectionWithPermissions = await _collectionRepository.GetByIdWithPermissionsAsync(collection.Id, _currentContext.UserId.Value, false);

        return new CollectionAccessDetailsResponseModel(collectionWithPermissions);
    }

    [HttpPut("{id}")]
    [HttpPost("{id}")]
    public async Task<CollectionResponseModel> Put(Guid orgId, Guid id, [FromBody] CollectionRequestModel model)
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

        if (!_currentContext.UserId.HasValue || (_currentContext.GetOrganization(collection.OrganizationId) == null && await _currentContext.ProviderUserForOrgAsync(collection.OrganizationId)))
        {
            return new CollectionAccessDetailsResponseModel(collection);
        }

        // If we have a user, fetch the latest collection permission details
        var collectionWithPermissions = await _collectionRepository.GetByIdWithPermissionsAsync(collection.Id, _currentContext.UserId.Value, false);

        return new CollectionAccessDetailsResponseModel(collectionWithPermissions);
    }

    [HttpPut("{id}/users")]
    public async Task PutUsers(Guid orgId, Guid id, [FromBody] IEnumerable<SelectionReadOnlyRequestModel> model)
    {
        var collection = await _collectionRepository.GetByIdAsync(id);
        var authorized = (await _authorizationService.AuthorizeAsync(User, collection, BulkCollectionOperations.ModifyUserAccess)).Succeeded;
        if (!authorized)
        {
            throw new NotFoundException();
        }

        await _collectionRepository.UpdateUsersAsync(collection.Id, model?.Select(g => g.ToSelectionReadOnly()));
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
    [HttpPost("{id}/delete")]
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

    [HttpDelete("")]
    [HttpPost("delete")]
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

    [HttpDelete("{id}/user/{orgUserId}")]
    [HttpPost("{id}/delete-user/{orgUserId}")]
    public async Task DeleteUser(Guid orgId, Guid id, Guid orgUserId)
    {
        var collection = await _collectionRepository.GetByIdAsync(id);
        var authorized = (await _authorizationService.AuthorizeAsync(User, collection, BulkCollectionOperations.ModifyUserAccess)).Succeeded;
        if (!authorized)
        {
            throw new NotFoundException();
        }

        await _collectionService.DeleteUserAsync(collection, orgUserId);
    }
}
