using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Context;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using Bit.Api.Models.Request;
using Bit.Api.Models.Response;

namespace Bit.Api.Controllers
{
    [Route("organizations/{orgId}/collections")]
    [Authorize("Application")]
    public class CollectionsController : Controller
    {
        private readonly ICollectionRepository _collectionRepository;
        private readonly ICollectionService _collectionService;
        private readonly IUserService _userService;
        private readonly ICurrentContext _currentContext;

        public CollectionsController(
            ICollectionRepository collectionRepository,
            ICollectionService collectionService,
            IUserService userService,
            ICurrentContext currentContext)
        {
            _collectionRepository = collectionRepository;
            _collectionService = collectionService;
            _userService = userService;
            _currentContext = currentContext;
        }

        [HttpGet("{id}")]
        public async Task<CollectionResponseModel> Get(string orgId, string id)
        {
            if (!await CanViewCollectionAsync(orgId, id))
            {
                throw new NotFoundException();
            }

            var collection = await GetCollectionAsync(new Guid(id), new Guid(orgId));
            return new CollectionResponseModel(collection);
        }

        [HttpGet("{id}/details")]
        public async Task<CollectionGroupDetailsResponseModel> GetDetails(string orgId, string id)
        {
            var orgIdGuid = new Guid(orgId);
            if (!await ViewAtLeastOneCollectionAsync(orgIdGuid) && !await _currentContext.ManageUsers(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var idGuid = new Guid(id);
            if (await _currentContext.ViewAllCollections(orgIdGuid))
            {
                var collectionDetails = await _collectionRepository.GetByIdWithGroupsAsync(idGuid);
                if (collectionDetails?.Item1 == null || collectionDetails.Item1.OrganizationId != orgIdGuid)
                {
                    throw new NotFoundException();
                }
                return new CollectionGroupDetailsResponseModel(collectionDetails.Item1, collectionDetails.Item2);
            }
            else
            {
                var collectionDetails = await _collectionRepository.GetByIdWithGroupsAsync(idGuid,
                    _currentContext.UserId.Value);
                if (collectionDetails?.Item1 == null || collectionDetails.Item1.OrganizationId != orgIdGuid)
                {
                    throw new NotFoundException();
                }
                return new CollectionGroupDetailsResponseModel(collectionDetails.Item1, collectionDetails.Item2);
            }
        }

        [HttpGet("")]
        public async Task<ListResponseModel<CollectionResponseModel>> Get(string orgId)
        {
            var orgIdGuid = new Guid(orgId);
            if (!await _currentContext.ViewAllCollections(orgIdGuid) && !await _currentContext.ManageUsers(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var collections = await _collectionRepository.GetManyByOrganizationIdAsync(orgIdGuid);
            var responses = collections.Select(c => new CollectionResponseModel(c));
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
        public async Task<IEnumerable<SelectionReadOnlyResponseModel>> GetUsers(string orgId, string id)
        {
            var collection = await GetCollectionAsync(new Guid(id), new Guid(orgId));
            var collectionUsers = await _collectionRepository.GetManyUsersByIdAsync(collection.Id);
            var responses = collectionUsers.Select(cu => new SelectionReadOnlyResponseModel(cu));
            return responses;
        }

        [HttpPost("")]
        public async Task<CollectionResponseModel> Post(string orgId, [FromBody]CollectionRequestModel model)
        {
            var orgIdGuid = new Guid(orgId);
            var collection = model.ToCollection(orgIdGuid);

            if (!await CanCreateCollection(orgIdGuid, collection.Id) &&
                !await CanEditCollectionAsync(orgIdGuid, collection.Id))
            {
                throw new NotFoundException();
            }

            var assignUserToCollection = !(await _currentContext.EditAnyCollection(orgIdGuid)) &&
                await _currentContext.EditAssignedCollections(orgIdGuid);

            await _collectionService.SaveAsync(collection, model.Groups?.Select(g => g.ToSelectionReadOnly()),
                assignUserToCollection ? _currentContext.UserId : null);
            return new CollectionResponseModel(collection);
        }

        [HttpPut("{id}")]
        [HttpPost("{id}")]
        public async Task<CollectionResponseModel> Put(string orgId, string id, [FromBody]CollectionRequestModel model)
        {
            if (!await CanEditCollectionAsync(orgId, id))
            {
                throw new NotFoundException();
            }

            var collection = await GetCollectionAsync(new Guid(id), new Guid(orgId));
            await _collectionService.SaveAsync(model.ToCollection(collection),
                model.Groups?.Select(g => g.ToSelectionReadOnly()));
            return new CollectionResponseModel(collection);
        }

        [HttpPut("{id}/users")]
        public async Task PutUsers(string orgId, string id, [FromBody]IEnumerable<SelectionReadOnlyRequestModel> model)
        {
            var collection = await GetCollectionAsync(new Guid(id), new Guid(orgId));
            await _collectionRepository.UpdateUsersAsync(collection.Id, model?.Select(g => g.ToSelectionReadOnly()));
        }

        [HttpDelete("{id}")]
        [HttpPost("{id}/delete")]
        public async Task Delete(string orgId, string id)
        {
            if (!await CanDeleteCollectionAsync(orgId, id))
            {
                throw new NotFoundException();
            }

            var collection = await GetCollectionAsync(new Guid(id), new Guid(orgId));
            await _collectionService.DeleteAsync(collection);
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


        public async Task<bool> CanCreateCollection(Guid orgId, Guid collectionId)
        {
            if (collectionId != default)
            {
                return false;
            }

            return await _currentContext.CreateNewCollections(orgId);
        }

        private async Task<bool> CanEditCollectionAsync(string orgId, string collectionId) =>
            await CanEditCollectionAsync(new Guid(orgId), new Guid(collectionId));
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
                return null != _collectionRepository.GetByIdAsync(collectionId, _currentContext.UserId.Value);
            }

            return false;
        }

        private async Task<bool> CanDeleteCollectionAsync(string orgId, string collectionId) =>
            await CanDeleteCollectionAsync(new Guid(orgId), new Guid(collectionId));
        private async Task<bool> CanDeleteCollectionAsync(Guid orgId, Guid collectionId)
        {
            if (collectionId == default)
            {
                return false;
            }

            if (await _currentContext.DeleteAnyCollection(orgId))
            {
                return true;
            }

            if (await _currentContext.DeleteAssignedCollections(orgId))
            {
                return null != _collectionRepository.GetByIdAsync(collectionId, _currentContext.UserId.Value);
            }

            return false;
        }

        private async Task<bool> CanViewCollectionAsync(string orgId, string collectionId) =>
            await CanViewCollectionAsync(new Guid(orgId), new Guid(collectionId));
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
                return null != _collectionRepository.GetByIdAsync(collectionId, _currentContext.UserId.Value);
            }

            return false;
        }

        private async Task<bool> ViewAtLeastOneCollectionAsync(Guid orgId)
        {
            return await _currentContext.ViewAllCollections(orgId) || await _currentContext.ViewAssignedCollections(orgId);
        }
    }
}
