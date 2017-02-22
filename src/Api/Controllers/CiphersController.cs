using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Bit.Api.Models;
using Bit.Core.Exceptions;
using Bit.Core.Domains;
using Microsoft.AspNetCore.Identity;
using Bit.Core.Services;

namespace Bit.Api.Controllers
{
    [Route("ciphers")]
    [Authorize("Application")]
    public class CiphersController : Controller
    {
        private readonly ICipherRepository _cipherRepository;
        private readonly ICipherService _cipherService;
        private readonly IUserService _userService;

        public CiphersController(
            ICipherRepository cipherRepository,
            ICipherService cipherService,
            IUserService userService)
        {
            _cipherRepository = cipherRepository;
            _cipherService = cipherService;
            _userService = userService;
        }

        [HttpGet("{id}")]
        public async Task<CipherShareResponseModel> Get(string id)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var cipher = await _cipherRepository.GetShareByIdAsync(new Guid(id), userId);
            if(cipher == null)
            {
                throw new NotFoundException();
            }

            return new CipherShareResponseModel(cipher, userId);
        }

        [HttpGet("")]
        public async Task<ListResponseModel<CipherShareResponseModel>> Get()
        {
            var userId = _userService.GetProperUserId(User).Value;
            var ciphers = await _cipherRepository.GetManyShareByUserIdAsync(userId);
            var responses = ciphers.Select(c => new CipherShareResponseModel(c, userId));
            return new ListResponseModel<CipherShareResponseModel>(responses);
        }

        [Obsolete]
        [HttpGet("history")]
        public async Task<CipherHistoryResponseModel> Get(DateTime since)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var history = await _cipherRepository.GetManySinceRevisionDateAndUserIdWithDeleteHistoryAsync(
                since, userId);
            return new CipherHistoryResponseModel(history.Item1, history.Item2, userId);
        }

        [HttpPost("import")]
        public async Task PostImport([FromBody]ImportRequestModel model)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var folderCiphers = model.Folders.Select(f => f.ToCipher(userId)).ToList();
            var otherCiphers = model.Logins.Select(s => s.ToCipher(userId)).ToList();

            await _cipherService.ImportCiphersAsync(
                folderCiphers,
                otherCiphers,
                model.FolderRelationships);
        }

        [HttpPut("{id}/favorite")]
        [HttpPost("{id}/favorite")]
        public async Task Favorite(string id)
        {
            var cipher = await _cipherRepository.GetByIdAsync(new Guid(id), _userService.GetProperUserId(User).Value);
            if(cipher == null)
            {
                throw new NotFoundException();
            }

            cipher.Favorite = !cipher.Favorite;

            await _cipherService.SaveAsync(cipher);
        }

        [HttpDelete("{id}")]
        [HttpPost("{id}/delete")]
        public async Task Delete(string id)
        {
            var cipher = await _cipherRepository.GetByIdAsync(new Guid(id), _userService.GetProperUserId(User).Value);
            if(cipher == null)
            {
                throw new NotFoundException();
            }

            await _cipherService.DeleteAsync(cipher);
        }
    }
}
