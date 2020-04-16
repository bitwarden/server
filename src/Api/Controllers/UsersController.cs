using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api;

namespace Bit.Api.Controllers
{
    [Route("users")]
    [Authorize("Application")]
    public class UsersController : Controller
    {
        private readonly IUserRepository _userRepository;

        public UsersController(
            IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        [HttpGet("{id}/public-key")]
        public async Task<UserKeyResponseModel> Get(string id)
        {
            var guidId = new Guid(id);
            var key = await _userRepository.GetPublicKeyAsync(guidId);
            if (key == null)
            {
                throw new NotFoundException();
            }

            return new UserKeyResponseModel(guidId, key);
        }
    }
}
