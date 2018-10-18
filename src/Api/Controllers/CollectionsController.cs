using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Bit.Core.Models.Api;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core;
using Bit.Core.Models.Table;
using System.Collections.Generic;

namespace Bit.Api.Controllers
{
    [Route("organizations/{orgId}/collections")]
    [Authorize("Application")]
    public class CollectionsController : Controller
    {
        private readonly ICollectionRepository _collectionRepository;
        private readonly ICollectionService _collectionService;
        private readonly IUserService _userService;
        private readonly CurrentContext _currentContext;

        public CollectionsController(
            ICollectionRepository collectionRepository,
            ICollectionService collectionService,
            IUserService userService,
            CurrentContext currentContext)
        {
            _collectionRepository = collectionRepository;
            _collectionService = collectionService;
            _userService = userService;
            _currentContext = currentContext;
        }

        [HttpGet("{id}")]
        public async Task<CollectionResponseModel> Get(string orgId, string id)
        {
            var collection = await GetCollectionAsync(new Guid(id), new Guid(orgId));
            return new CollectionResponseModel(collection);
        }

        [HttpGet("{id}/details")]
        public async Task<CollectionGroupDetailsResponseModel> GetDetails(string orgId, string id)
        {
            var orgIdGuid = new Guid(orgId);
            if(!_currentContext.OrganizationManager(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var idGuid = new Guid(id);
            if(_currentContext.OrganizationAdmin(orgIdGuid))
            {
                var collectionDetails = await _collectionRepository.GetByIdWithGroupsAsync(idGuid);
                if(collectionDetails?.Item1 == null || collectionDetails.Item1.OrganizationId != orgIdGuid)
                {
                    throw new NotFoundException();
                }
                return new CollectionGroupDetailsResponseModel(collectionDetails.Item1, collectionDetails.Item2);
            }
            else
            {
                var collectionDetails = await _collectionRepository.GetByIdWithGroupsAsync(idGuid,
                    _currentContext.UserId.Value);
                if(collectionDetails?.Item1 == null || collectionDetails.Item1.OrganizationId != orgIdGuid)
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
            if(!_currentContext.OrganizationAdmin(orgIdGuid))
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
            if(!_currentContext.OrganizationManager(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var collection = model.ToCollection(orgIdGuid);
            await _collectionService.SaveAsync(collection, model.Groups?.Select(g => g.ToSelectionReadOnly()),
                !_currentContext.OrganizationAdmin(orgIdGuid) ? _currentContext.UserId : null);
            return new CollectionResponseModel(collection);
        }

        [HttpPut("{id}")]
        [HttpPost("{id}")]
        public async Task<CollectionResponseModel> Put(string orgId, string id, [FromBody]CollectionRequestModel model)
        {
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
            if(!_currentContext.OrganizationManager(orgId))
            {
                throw new NotFoundException();
            }

            var collection = _currentContext.OrganizationAdmin(orgId) ?
                await _collectionRepository.GetByIdAsync(id) :
                await _collectionRepository.GetByIdAsync(id, _currentContext.UserId.Value);
            if(collection == null || collection.OrganizationId != orgId)
            {
                throw new NotFoundException();
            }

            return collection;
        }
    }
}
