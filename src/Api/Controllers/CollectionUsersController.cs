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
    [Route("organizations/{orgId}/collectionUsers")]
    [Authorize("Application")]
    public class CollectionUsersController : Controller
    {
        private readonly ICollectionRepository _collectionRepository;
        private readonly ICollectionUserRepository _collectionUserRepository;
        private readonly IUserService _userService;
        private readonly CurrentContext _currentContext;

        public CollectionUsersController(
            ICollectionRepository collectionRepository,
            ICollectionUserRepository collectionUserRepository,
            IUserService userService,
            CurrentContext currentContext)
        {
            _collectionRepository = collectionRepository;
            _collectionUserRepository = collectionUserRepository;
            _userService = userService;
            _currentContext = currentContext;
        }

        [HttpGet("{collectionId}")]
        public async Task<ListResponseModel<CollectionUserResponseModel>> GetByCollection(string orgId, string collectionId)
        {
            var collectionIdGuid = new Guid(collectionId);
            var collection = await _collectionRepository.GetByIdAsync(collectionIdGuid);
            if(collection == null || !_currentContext.OrganizationAdmin(collection.OrganizationId))
            {
                throw new NotFoundException();
            }

            var collectionUsers = await _collectionUserRepository.GetManyDetailsByCollectionIdAsync(collectionIdGuid);
            var responses = collectionUsers.Select(c => new CollectionUserResponseModel(c));
            return new ListResponseModel<CollectionUserResponseModel>(responses);
        }

        [HttpDelete("{id}")]
        [HttpPost("{id}/delete")]
        public async Task Delete(string orgId, string id)
        {
            var user = await _collectionUserRepository.GetByIdAsync(new Guid(id));
            if(user == null)
            {
                throw new NotFoundException();
            }

            var collection = await _collectionRepository.GetByIdAsync(user.CollectionId);
            if(collection == null || !_currentContext.OrganizationAdmin(collection.OrganizationId))
            {
                throw new NotFoundException();
            }

            await _collectionUserRepository.DeleteAsync(user);
        }
    }
}
