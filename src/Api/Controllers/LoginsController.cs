using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Bit.Core.Models.Api;
using Bit.Core.Exceptions;
using Bit.Core.Services;

namespace Bit.Api.Controllers
{
    [Route("logins")]
    // "sites" route is deprecated
    [Route("sites")]
    [Authorize("Application")]
    public class LoginsController : Controller
    {
        private readonly ICipherRepository _cipherRepository;
        private readonly ICipherService _cipherService;
        private readonly IUserService _userService;

        public LoginsController(
            ICipherRepository cipherRepository,
            ICipherService cipherService,
            IUserService userService)
        {
            _cipherRepository = cipherRepository;
            _cipherService = cipherService;
            _userService = userService;
        }

        [HttpGet("{id}")]
        public async Task<LoginResponseModel> Get(string id)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var login = await _cipherRepository.GetByIdAsync(new Guid(id), userId);
            if(login == null || login.Type != Core.Enums.CipherType.Login)
            {
                throw new NotFoundException();
            }

            var response = new LoginResponseModel(login);
            return response;
        }

        [HttpGet("")]
        public async Task<ListResponseModel<LoginResponseModel>> Get()
        {
            var userId = _userService.GetProperUserId(User).Value;
            var logins = await _cipherRepository.GetManyByTypeAndUserIdAsync(Core.Enums.CipherType.Login,
                userId);
            var responses = logins.Select(s => new LoginResponseModel(s)).ToList();
            return new ListResponseModel<LoginResponseModel>(responses);
        }

        [HttpPost("")]
        public async Task<LoginResponseModel> Post([FromBody]LoginRequestModel model)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var login = model.ToCipherDetails(userId);
            await _cipherService.SaveAsync(login);

            var response = new LoginResponseModel(login);
            return response;
        }

        [HttpPut("{id}")]
        [HttpPost("{id}")]
        public async Task<LoginResponseModel> Put(string id, [FromBody]LoginRequestModel model)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var login = await _cipherRepository.GetByIdAsync(new Guid(id), _userService.GetProperUserId(User).Value);
            if(login == null || login.Type != Core.Enums.CipherType.Login)
            {
                throw new NotFoundException();
            }

            await _cipherService.SaveAsync(model.ToCipherDetails(login));

            var response = new LoginResponseModel(login);
            return response;
        }

        [HttpDelete("{id}")]
        [HttpPost("{id}/delete")]
        public async Task Delete(string id)
        {
            var login = await _cipherRepository.GetByIdAsync(new Guid(id), _userService.GetProperUserId(User).Value);
            if(login == null || login.Type != Core.Enums.CipherType.Login)
            {
                throw new NotFoundException();
            }

            await _cipherService.DeleteAsync(login);
        }
    }
}
