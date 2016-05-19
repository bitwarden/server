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
        private readonly IFolderRepository _folderRepository;
        private readonly UserManager<User> _userManager;

        public FoldersController(
            IFolderRepository folderRepository,
            UserManager<User> userManager)
        {
            _folderRepository = folderRepository;
            _userManager = userManager;
        }

        [HttpGet("{id}")]
        public async Task<FolderResponseModel> Get(string id)
        {
            var folder = await _folderRepository.GetByIdAsync(id, _userManager.GetUserId(User));
            if(folder == null)
            {
                throw new NotFoundException();
            }

            return new FolderResponseModel(folder);
        }

        [HttpGet("")]
        public async Task<ListResponseModel<FolderResponseModel>> Get()
        {
            ICollection<Folder> folders = await _folderRepository.GetManyByUserIdAsync(_userManager.GetUserId(User));
            var responses = folders.Select(f => new FolderResponseModel(f));
            return new ListResponseModel<FolderResponseModel>(responses);
        }

        [HttpPost("")]
        public async Task<FolderResponseModel> Post([FromBody]FolderRequestModel model)
        {
            var folder = model.ToFolder(_userManager.GetUserId(User));
            await _folderRepository.CreateAsync(folder);
            return new FolderResponseModel(folder);
        }

        [HttpPut("{id}")]
        public async Task<FolderResponseModel> Put(string id, [FromBody]FolderRequestModel model)
        {
            var folder = await _folderRepository.GetByIdAsync(id, _userManager.GetUserId(User));
            if(folder == null)
            {
                throw new NotFoundException();
            }

            await _folderRepository.ReplaceAsync(model.ToFolder(folder));
            return new FolderResponseModel(folder);
        }

        [HttpDelete("{id}")]
        public async Task Delete(string id)
        {
            var folder = await _folderRepository.GetByIdAsync(id, _userManager.GetUserId(User));
            if(folder == null)
            {
                throw new NotFoundException();
            }

            await _folderRepository.DeleteAsync(folder);
        }
    }
}
