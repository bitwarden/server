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
        private readonly UserManager<User> _userManager;

        public CiphersController(
            ICipherRepository cipherRepository,
            ICipherService cipherService,
            UserManager<User> userManager)
        {
            _cipherRepository = cipherRepository;
            _cipherService = cipherService;
            _userManager = userManager;
        }

        [HttpGet("{id}")]
        public async Task<CipherResponseModel> Get(string id)
        {
            var cipher = await _cipherRepository.GetByIdAsync(new Guid(id), new Guid(_userManager.GetUserId(User)));
            if(cipher == null)
            {
                throw new NotFoundException();
            }

            return new CipherResponseModel(cipher);
        }

        [HttpGet("")]
        public async Task<ListResponseModel<CipherResponseModel>> Get()
        {
            var ciphers = await _cipherRepository.GetManyByUserIdAsync(new Guid(_userManager.GetUserId(User)));
            var responses = ciphers.Select(c => new CipherResponseModel(c));
            return new ListResponseModel<CipherResponseModel>(responses);
        }

        [HttpGet("history")]
        public async Task<CipherHistoryResponseModel> Get(DateTime since)
        {
            var history = await _cipherRepository.GetManySinceRevisionDateAndUserIdWithDeleteHistoryAsync(
                since, new Guid(_userManager.GetUserId(User)));
            return new CipherHistoryResponseModel(history.Item1, history.Item2);
        }

        [HttpPost("import")]
        public async Task PostImport([FromBody]ImportRequestModel model)
        {
            var folderCiphers = model.Folders.Select(f => f.ToCipher(_userManager.GetUserId(User))).ToList();
            var otherCiphers = model.Sites.Select(s => s.ToCipher(_userManager.GetUserId(User))).ToList();

            await _cipherService.ImportCiphersAsync(
                folderCiphers,
                otherCiphers,
                model.FolderRelationships);
        }

        [HttpDelete("{id}")]
        public async Task Delete(string id)
        {
            var cipher = await _cipherRepository.GetByIdAsync(new Guid(id), new Guid(_userManager.GetUserId(User)));
            if(cipher == null)
            {
                throw new NotFoundException();
            }

            await _cipherRepository.DeleteAsync(cipher);
        }
    }
}
