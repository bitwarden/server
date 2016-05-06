using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Bit.Core.Repositories;
using System.Security.Claims;
using Microsoft.AspNet.Authorization;
using Bit.Api.Models;
using Bit.Core.Exceptions;
using Bit.Core.Domains;

namespace Bit.Api.Controllers
{
    [Route("folders")]
    [Authorize("Application")]
    public class FoldersController : Controller
    {
        private readonly IFolderRepository _folderRepository;

        public FoldersController(
            IFolderRepository folderRepository)
        {
            _folderRepository = folderRepository;
        }

        [HttpGet("{id}")]
        public async Task<FolderResponseModel> Get(string id)
        {
            var folder = await _folderRepository.GetByIdAsync(id, User.GetUserId());
            if(folder == null)
            {
                throw new NotFoundException();
            }

            return new FolderResponseModel(folder);
        }

        [HttpGet("")]
        public async Task<ListResponseModel<FolderResponseModel>> Get()
        {
            ICollection<Folder> folders = await _folderRepository.GetManyByUserIdAsync(User.GetUserId());
            var responses = folders.Select(f => new FolderResponseModel(f));
            return new ListResponseModel<FolderResponseModel>(responses);
        }

        [HttpPost("")]
        public async Task<FolderResponseModel> Post([FromBody]FolderRequestModel model)
        {
            var folder = model.ToFolder(User.GetUserId());
            await _folderRepository.CreateAsync(folder);
            return new FolderResponseModel(folder);
        }

        [HttpPut("{id}")]
        public async Task<FolderResponseModel> Put(string id, [FromBody]FolderRequestModel model)
        {
            var folder = await _folderRepository.GetByIdAsync(id, User.GetUserId());
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
            var folder = await _folderRepository.GetByIdAsync(id, User.GetUserId());
            if(folder == null)
            {
                throw new NotFoundException();
            }

            await _folderRepository.DeleteAsync(folder);
        }
    }
}
