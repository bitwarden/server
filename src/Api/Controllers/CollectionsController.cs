using Bit.Api.Models.Request;
using Bit.Api.Models.Response;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationCollections.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
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
    private readonly ICurrentContext _currentContext;

    public CollectionsController(
        ICollectionRepository collectionRepository,
        ICollectionService collectionService,
        IDeleteCollectionCommand deleteCollectionCommand,
        IUserService userService,
        ICurrentContext currentContext)
    {
        _collectionRepository = collectionRepository;
        _collectionService = collectionService;
        _deleteCollectionCommand = deleteCollectionCommand;
        _userService = userService;
        _currentContext = currentContext;
    }

    [HttpGet("{id}")]
    public async Task<CollectionResponseModel> Get(Guid orgId, Guid id)
    {
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
                _currentContext.UserId.Value);
            if (collection == null || collection.OrganizationId != orgId)
            {
                throw new NotFoundException();
            }
            return new CollectionAccessDetailsResponseModel(collection, access.Groups, access.Users);
        }
    }

    [HttpGet("details")]
    public async Task<ListResponseModel<CollectionGroupDetailsResponseModel>> GetManyWithDetails(Guid orgId)
    {
        if (!await ViewAtLeastOneCollectionAsync(orgId) && !await _currentContext.ManageUsers(orgId))
        {
            throw new NotFoundException();
        }

        var collectionDetails = await _collectionService.GetOrganizationCollectionsWithGroups(orgId);

        var responses = collectionDetails.Select(d => new CollectionGroupDetailsResponseModel(d.Item1, d.Item2));
        return new ListResponseModel<CollectionGroupDetailsResponseModel>(responses);
    }

    [HttpGet("")]
    public async Task<ListResponseModel<CollectionResponseModel>> Get(Guid orgId)
    {
        IEnumerable<Collection> orgCollections = await _collectionService.GetOrganizationCollections(orgId);

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
        var collection = await GetCollectionAsync(id, orgId);
        var collectionUsers = await _collectionRepository.GetManyUsersByIdAsync(collection.Id);
        var responses = collectionUsers.Select(cu => new SelectionReadOnlyResponseModel(cu));
        return responses;
    }

    [HttpPost("")]
    public async Task<CollectionResponseModel> Post(Guid orgId, [FromBody] CollectionRequestModel model)
    {
        var collection = model.ToCollection(orgId);

        if (!await CanCreateCollection(orgId, collection.Id) &&
            !await CanEditCollectionAsync(orgId, collection.Id))
        {
            throw new NotFoundException();
        }

        var groups = model.Groups?.Select(g => g.ToSelectionReadOnly());
        var users = model.Users?.Select(g => g.ToSelectionReadOnly());

        var assignUserToCollection = !(await _currentContext.EditAnyCollection(orgId)) &&
            await _currentContext.EditAssignedCollections(orgId);

        await _collectionService.SaveAsync(collection, groups, users, assignUserToCollection ? _currentContext.UserId : null);
        return new CollectionResponseModel(collection);
    }

    [HttpPut("{id}")]
    [HttpPost("{id}")]
    public async Task<CollectionResponseModel> Put(Guid orgId, Guid id, [FromBody] CollectionRequestModel model)
    {
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
        if (!await CanEditCollectionAsync(orgId, id))
        {
            throw new NotFoundException();
        }

        var collection = await GetCollectionAsync(id, orgId);
        await _collectionRepository.UpdateUsersAsync(collection.Id, model?.Select(g => g.ToSelectionReadOnly()));
    }

    [HttpDelete("{id}")]
    [HttpPost("{id}/delete")]
    public async Task Delete(Guid orgId, Guid id)
    {
        if (!await CanDeleteCollectionAsync(orgId, new[] { id }))
        {
            throw new NotFoundException();
        }

        var collection = await GetCollectionAsync(id, orgId);
        await _deleteCollectionCommand.DeleteAsync(collection);
    }

    [HttpDelete("")]
    [HttpPost("delete")]
    public async Task DeleteMany([FromBody] CollectionBulkDeleteRequestModel model)
    {
        if (!await _currentContext.DeleteAssignedCollections(new Guid(model.OrganizationId)))
        {
            throw new NotFoundException();
        }

        await _deleteCollectionCommand.DeleteManyAsync(new Guid(model.OrganizationId), model.Ids.Select(i => new Guid(i)));
    }

    [HttpDelete("{id}/user/{orgUserId}")]
    [HttpPost("{id}/delete-user/{orgUserId}")]
    public async Task Delete(string orgId, string id, string orgUserId)
    {
        var collection = await GetCollectionAsync(new Guid(id), new Guid(orgId));
        await _collectionService.DeleteUserAsync(collection, new Guid(orgUserId));
    }

    private async Task<Collection> GetCollectionAsync(Guid id, Guid orgId)
    {
        Collection collection = default;
        if (await _currentContext.ViewAllCollections(orgId))
        {
            collection = await _collectionRepository.GetByIdAsync(id);
        }
        else if (await _currentContext.ViewAssignedCollections(orgId))
        {
            collection = await _collectionRepository.GetByIdAsync(id, _currentContext.UserId.Value);
        }

        if (collection == null || collection.OrganizationId != orgId)
        {
            throw new NotFoundException();
        }

        return collection;
    }


    private async Task<bool> CanCreateCollection(Guid orgId, Guid collectionId)
    {
        if (collectionId != default)
        {
            return false;
        }

        return await _currentContext.CreateNewCollections(orgId);
    }

    private async Task<bool> CanEditCollectionAsync(Guid orgId, Guid collectionId)
    {
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
            var collectionDetails = await _collectionRepository.GetByIdAsync(collectionId, _currentContext.UserId.Value);
            return collectionDetails != null;
        }

        return false;
    }

    private async Task<bool> CanDeleteCollectionAsync(Guid orgId, IEnumerable<Guid> collectionIds)
    {
        if (collectionIds == default)
        {
            return false;
        }

        if (await _currentContext.DeleteAnyCollection(orgId))
        {
            return true;
        }

        if (await _currentContext.DeleteAssignedCollections(orgId))
        {
            var collectionDetails = await _collectionRepository.GetManyByManyIdsAsync(collectionIds);
            return collectionDetails != null;
        }

        return false;
    }

    private async Task<bool> CanViewCollectionAsync(Guid orgId, Guid collectionId)
    {
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
            var collectionDetails = await _collectionRepository.GetByIdAsync(collectionId, _currentContext.UserId.Value);
            return collectionDetails != null;
        }

        return false;
    }

    private async Task<bool> ViewAtLeastOneCollectionAsync(Guid orgId)
    {
        return await _currentContext.ViewAllCollections(orgId) || await _currentContext.ViewAssignedCollections(orgId);
    }
}
