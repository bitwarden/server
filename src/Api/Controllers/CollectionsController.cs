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

namespace Bit.Api.Controllers
{
    [Route("organizations/{orgId}/collections")]
    [Authorize("Application")]
    public class CollectionsController : Controller
    {
        private readonly ICollectionRepository _collectionRepository;
        private readonly ICollectionUserRepository _collectionUserRepository;
        private readonly ICollectionService _collectionService;
        private readonly IUserService _userService;
        private readonly CurrentContext _currentContext;

        public CollectionsController(
            ICollectionRepository collectionRepository,
            ICollectionUserRepository collectionUserRepository,
            ICollectionService collectionService,
            IUserService userService,
            CurrentContext currentContext)
        {
            _collectionRepository = collectionRepository;
            _collectionUserRepository = collectionUserRepository;
            _collectionService = collectionService;
            _userService = userService;
            _currentContext = currentContext;
        }

        [HttpGet("{id}")]
        public async Task<CollectionResponseModel> Get(string orgId, string id)
        {
            var collection = await _collectionRepository.GetByIdAsync(new Guid(id));
            if(collection == null || !_currentContext.OrganizationAdmin(collection.OrganizationId))
            {
                throw new NotFoundException();
            }

            return new CollectionResponseModel(collection);
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
        public async Task<ListResponseModel<CollectionUserDetailsResponseModel>> GetUser()
        {
            var collections = await _collectionUserRepository.GetManyDetailsByUserIdAsync(
                _userService.GetProperUserId(User).Value);
            var responses = collections.Select(c => new CollectionUserDetailsResponseModel(c));
            return new ListResponseModel<CollectionUserDetailsResponseModel>(responses);
        }

        [HttpPost("")]
        public async Task<CollectionResponseModel> Post(string orgId, [FromBody]CollectionRequestModel model)
        {
            var orgIdGuid = new Guid(orgId);
            if(!_currentContext.OrganizationAdmin(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var collection = model.ToCollection(orgIdGuid);
            await _collectionService.SaveAsync(collection);
            return new CollectionResponseModel(collection);
        }

        [HttpPut("{id}")]
        [HttpPost("{id}")]
        public async Task<CollectionResponseModel> Put(string orgId, string id, [FromBody]CollectionRequestModel model)
        {
            var collection = await _collectionRepository.GetByIdAsync(new Guid(id));
            if(collection == null || !_currentContext.OrganizationAdmin(collection.OrganizationId))
            {
                throw new NotFoundException();
            }

            await _collectionService.SaveAsync(model.ToCollection(collection));
            return new CollectionResponseModel(collection);
        }

        [HttpDelete("{id}")]
        [HttpPost("{id}/delete")]
        public async Task Delete(string orgId, string id)
        {
            var collection = await _collectionRepository.GetByIdAsync(new Guid(id));
            if(collection == null || !_currentContext.OrganizationAdmin(collection.OrganizationId))
            {
                throw new NotFoundException();
            }

            await _collectionRepository.DeleteAsync(collection);
        }
    }
}
