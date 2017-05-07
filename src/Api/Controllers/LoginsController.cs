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
using System.Collections.Generic;
using Core.Models.Data;
using Bit.Core.Models.Table;

namespace Bit.Api.Controllers
{
    [Route("logins")]
    // "sites" route is deprecated
    [Route("sites")]
    [Authorize("Application")]
    public class LoginsController : Controller
    {
        private readonly ICipherRepository _cipherRepository;
        private readonly IFolderRepository _folderRepository;
        private readonly ICipherService _cipherService;
        private readonly IUserService _userService;
        private readonly CurrentContext _currentContext;

        public LoginsController(
            ICipherRepository cipherRepository,
            IFolderRepository folderRepository,
            ICipherService cipherService,
            IUserService userService,
            CurrentContext currentContext)
        {
            _cipherRepository = cipherRepository;
            _folderRepository = folderRepository;
            _cipherService = cipherService;
            _userService = userService;
            _currentContext = currentContext;
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

        [HttpGet("{id}/admin")]
        public async Task<LoginResponseModel> GetAdmin(string id)
        {
            var login = await _cipherRepository.GetByIdAsync(new Guid(id));
            if(login == null || !login.OrganizationId.HasValue ||
                !_currentContext.OrganizationAdmin(login.OrganizationId.Value))
            {
                throw new NotFoundException();
            }

            var response = new LoginResponseModel(login);
            return response;
        }

        [HttpGet("")]
        public async Task<ListResponseModel<LoginResponseModel>> Get(string[] expand = null)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var logins = await _cipherRepository.GetManyByTypeAndUserIdAsync(Core.Enums.CipherType.Login,
                userId);
            var responses = logins.Select(l => new LoginResponseModel(l)).ToList();
            await ExpandManyAsync(logins, responses, expand, userId);
            return new ListResponseModel<LoginResponseModel>(responses);
        }

        [HttpPost("")]
        public async Task<LoginResponseModel> Post([FromBody]LoginRequestModel model)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var login = model.ToCipherDetails(userId);
            await _cipherService.SaveDetailsAsync(login, userId);

            var response = new LoginResponseModel(login);
            return response;
        }

        [HttpPost("admin")]
        public async Task<LoginResponseModel> PostAdmin([FromBody]LoginRequestModel model)
        {
            var login = model.ToOrganizationCipher();
            if(!_currentContext.OrganizationAdmin(login.OrganizationId.Value))
            {
                throw new NotFoundException();
            }

            var userId = _userService.GetProperUserId(User).Value;
            await _cipherService.SaveAsync(login, userId, true);

            var response = new LoginResponseModel(login);
            return response;
        }

        [HttpPut("{id}")]
        [HttpPost("{id}")]
        public async Task<LoginResponseModel> Put(string id, [FromBody]LoginRequestModel model)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var login = await _cipherRepository.GetByIdAsync(new Guid(id), userId);
            if(login == null || login.Type != Core.Enums.CipherType.Login)
            {
                throw new NotFoundException();
            }

            await _cipherService.SaveDetailsAsync(model.ToCipherDetails(login), userId);

            var response = new LoginResponseModel(login);
            return response;
        }

        [HttpPut("{id}/admin")]
        [HttpPost("{id}/admin")]
        public async Task<LoginResponseModel> PutAdmin(string id, [FromBody]LoginRequestModel model)
        {
            var login = await _cipherRepository.GetByIdAsync(new Guid(id));
            if(login == null || !login.OrganizationId.HasValue ||
                !_currentContext.OrganizationAdmin(login.OrganizationId.Value))
            {
                throw new NotFoundException();
            }

            var userId = _userService.GetProperUserId(User).Value;
            await _cipherService.SaveAsync(model.ToCipher(login), userId, true);

            var response = new LoginResponseModel(login);
            return response;
        }

        [HttpDelete("{id}")]
        [HttpPost("{id}/delete")]
        public async Task Delete(string id)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var login = await _cipherRepository.GetByIdAsync(new Guid(id), userId);
            if(login == null || login.Type != Core.Enums.CipherType.Login)
            {
                throw new NotFoundException();
            }

            await _cipherService.DeleteAsync(login, userId);
        }

        [Obsolete]
        private async Task ExpandManyAsync(IEnumerable<CipherDetails> logins, ICollection<LoginResponseModel> responses,
            string[] expand, Guid userId)
        {
            if(expand == null || expand.Count() == 0)
            {
                return;
            }

            if(expand.Any(e => e.ToLower() == "folder"))
            {
                var folders = await _folderRepository.GetManyByUserIdAsync(userId);
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
