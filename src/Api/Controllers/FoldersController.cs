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

namespace Bit.Api.Controllers
{
    [Route("folders")]
    [Authorize("Application")]
    public class FoldersController : Controller
    {
        private readonly ICipherRepository _cipherRepository;
        private readonly UserManager<User> _userManager;

        public FoldersController(
            ICipherRepository cipherRepository,
            UserManager<User> userManager)
        {
            _cipherRepository = cipherRepository;
            _userManager = userManager;
        }

        [HttpGet("{id}")]
        public async Task<FolderResponseModel> Get(string id)
        {
            var folder = await _cipherRepository.GetByIdAsync(new Guid(id), new Guid(_userManager.GetUserId(User)));
            if(folder == null || folder.Type != Core.Enums.CipherType.Folder)
            {
                throw new NotFoundException();
            }

            return new FolderResponseModel(folder);
        }

        [HttpGet("")]
        public async Task<ListResponseModel<FolderResponseModel>> Get()
        {
            ICollection<Cipher> folders = await _cipherRepository.GetManyByTypeAndUserIdAsync(Core.Enums.CipherType.Folder, new Guid(_userManager.GetUserId(User)));
            var responses = folders.Select(f => new FolderResponseModel(f));
            return new ListResponseModel<FolderResponseModel>(responses);
        }

        [HttpPost("")]
        public async Task<FolderResponseModel> Post([FromBody]FolderRequestModel model)
        {
            var folder = model.ToCipher(_userManager.GetUserId(User));
            await _cipherRepository.CreateAsync(folder);
            return new FolderResponseModel(folder);
        }

        [HttpPut("{id}")]
        public async Task<FolderResponseModel> Put(string id, [FromBody]FolderRequestModel model)
        {
            var folder = await _cipherRepository.GetByIdAsync(new Guid(id), new Guid(_userManager.GetUserId(User)));
            if(folder == null || folder.Type != Core.Enums.CipherType.Folder)
            {
                throw new NotFoundException();
            }

            await _cipherRepository.ReplaceAsync(model.ToCipher(folder));
            return new FolderResponseModel(folder);
        }

        [HttpDelete("{id}")]
        public async Task Delete(string id)
        {
            var folder = await _cipherRepository.GetByIdAsync(new Guid(id), new Guid(_userManager.GetUserId(User)));
            if(folder == null || folder.Type != Core.Enums.CipherType.Folder)
            {
                throw new NotFoundException();
            }

            await _cipherRepository.DeleteAsync(folder);
        }
    }
}
