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
    [Route("logins")]
    // "sites" route is deprecated
    [Route("sites")]
    [Authorize("Application")]
    public class LoginsController : Controller
    {
        private readonly ICipherRepository _cipherRepository;
        private readonly ICipherService _cipherService;
        private readonly UserManager<User> _userManager;

        public LoginsController(
            ICipherRepository cipherRepository,
            ICipherService cipherService,
            UserManager<User> userManager)
        {
            _cipherRepository = cipherRepository;
            _cipherService = cipherService;
            _userManager = userManager;
        }

        [HttpGet("{id}")]
        public async Task<LoginResponseModel> Get(string id, string[] expand = null)
        {
            var login = await _cipherRepository.GetByIdAsync(new Guid(id), new Guid(_userManager.GetUserId(User)));
            if(login == null || login.Type != Core.Enums.CipherType.Login)
            {
                throw new NotFoundException();
            }

            var response = new LoginResponseModel(login);
            await ExpandAsync(login, response, expand, null);
            return response;
        }

        [HttpGet("")]
        public async Task<ListResponseModel<LoginResponseModel>> Get(string[] expand = null)
        {
            ICollection<Cipher> logins = await _cipherRepository.GetManyByTypeAndUserIdAsync(Core.Enums.CipherType.Login,
                new Guid(_userManager.GetUserId(User)));
            var responses = logins.Select(s => new LoginResponseModel(s)).ToList();
            await ExpandManyAsync(logins, responses, expand, null);
            return new ListResponseModel<LoginResponseModel>(responses);
        }

        [HttpPost("")]
        public async Task<LoginResponseModel> Post([FromBody]LoginRequestModel model, string[] expand = null)
        {
            var login = model.ToCipher(_userManager.GetUserId(User));
            await _cipherService.SaveAsync(login);

            var response = new LoginResponseModel(login);
            await ExpandAsync(login, response, expand, null);
            return response;
        }

        [HttpPut("{id}")]
        [HttpPost("{id}")]
        public async Task<LoginResponseModel> Put(string id, [FromBody]LoginRequestModel model, string[] expand = null)
        {
            var login = await _cipherRepository.GetByIdAsync(new Guid(id), new Guid(_userManager.GetUserId(User)));
            if(login == null || login.Type != Core.Enums.CipherType.Login)
            {
                throw new NotFoundException();
            }

            await _cipherService.SaveAsync(model.ToCipher(login));

            var response = new LoginResponseModel(login);
            await ExpandAsync(login, response, expand, null);
            return response;
        }

        [HttpDelete("{id}")]
        [HttpPost("{id}/delete")]
        public async Task Delete(string id)
        {
            var login = await _cipherRepository.GetByIdAsync(new Guid(id), new Guid(_userManager.GetUserId(User)));
            if(login == null || login.Type != Core.Enums.CipherType.Login)
            {
                throw new NotFoundException();
            }

            await _cipherService.DeleteAsync(login);
        }

        private async Task ExpandAsync(Cipher login, LoginResponseModel response, string[] expand, Cipher folder)
        {
            if(expand == null || expand.Count() == 0)
            {
                return;
            }

            if(expand.Any(e => e.ToLower() == "folder") && login.FolderId.HasValue)
            {
                if(folder == null)
                {
                    folder = await _cipherRepository.GetByIdAsync(login.FolderId.Value);
                }

                response.Folder = new FolderResponseModel(folder);
            }
        }

        private async Task ExpandManyAsync(IEnumerable<Cipher> logins, ICollection<LoginResponseModel> responses,
            string[] expand, IEnumerable<Cipher> folders)
        {
            if(expand == null || expand.Count() == 0)
            {
                return;
            }

            if(expand.Any(e => e.ToLower() == "folder"))
            {
                if(folders == null)
                {
                    folders = await _cipherRepository.GetManyByTypeAndUserIdAsync(Core.Enums.CipherType.Folder,
                        new Guid(_userManager.GetUserId(User)));
                }

                if(folders != null && folders.Count() > 0)
                {
                    foreach(var response in responses)
                    {
                        var login = logins.SingleOrDefault(s => s.Id.ToString() == response.Id);
                        if(login == null)
                        {
                            continue;
                        }

                        var folder = folders.SingleOrDefault(f => f.Id == login.FolderId);
                        if(folder == null)
                        {
                            continue;
                        }

                        response.Folder = new FolderResponseModel(folder);
                    }
                }
            }
        }
    }
}
