using System;
using System.Collections.Generic;
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
    [Route("folders")]
    [Authorize("Application")]
    public class FoldersController : Controller
    {
        private readonly ICipherRepository _cipherRepository;
        private readonly ICipherService _cipherService;
        private readonly IUserService _userService;

        public FoldersController(
            ICipherRepository cipherRepository,
            ICipherService cipherService,
            IUserService userService)
        {
            _cipherRepository = cipherRepository;
            _cipherService = cipherService;
            _userService = userService;
        }

        [HttpGet("{id}")]
        public async Task<FolderResponseModel> Get(string id)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var folder = await _cipherRepository.GetByIdAsync(new Guid(id), userId);
            if(folder == null || folder.Type != Core.Enums.CipherType.Folder)
            {
                throw new NotFoundException();
            }

            return new FolderResponseModel(folder, userId);
        }

        [HttpGet("")]
        public async Task<ListResponseModel<FolderResponseModel>> Get()
        {
            var userId = _userService.GetProperUserId(User).Value;
            ICollection<Cipher> folders = await _cipherRepository.GetManyByTypeAndUserIdAsync(Core.Enums.CipherType.Folder,
                userId);
            var responses = folders.Select(f => new FolderResponseModel(f, userId));
            return new ListResponseModel<FolderResponseModel>(responses);
        }

        [HttpPost("")]
        public async Task<FolderResponseModel> Post([FromBody]FolderRequestModel model)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var folder = model.ToCipher(_userService.GetProperUserId(User).Value);
            await _cipherService.SaveAsync(folder);
            return new FolderResponseModel(folder, userId);
        }

        [HttpPut("{id}")]
        [HttpPost("{id}")]
        public async Task<FolderResponseModel> Put(string id, [FromBody]FolderRequestModel model)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var folder = await _cipherRepository.GetByIdAsync(new Guid(id), userId);
            if(folder == null || folder.Type != Core.Enums.CipherType.Folder)
            {
                throw new NotFoundException();
            }

            await _cipherService.SaveAsync(model.ToCipher(folder));
            return new FolderResponseModel(folder, userId);
        }

        [HttpDelete("{id}")]
        [HttpPost("{id}/delete")]
        public async Task Delete(string id)
        {
            var folder = await _cipherRepository.GetByIdAsync(new Guid(id), _userService.GetProperUserId(User).Value);
            if(folder == null || folder.Type != Core.Enums.CipherType.Folder)
            {
                throw new NotFoundException();
            }

            await _cipherService.DeleteAsync(folder);
        }
    }
}
