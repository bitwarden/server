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
    [Route("sites")]
    [Authorize("Application")]
    public class SitesController : Controller
    {
        private readonly ICipherRepository _cipherRepository;
        private readonly ICipherService _cipherService;
        private readonly UserManager<User> _userManager;

        public SitesController(
            ICipherRepository cipherRepository,
            ICipherService cipherService,
            UserManager<User> userManager)
        {
            _cipherRepository = cipherRepository;
            _cipherService = cipherService;
            _userManager = userManager;
        }

        [HttpGet("{id}")]
        public async Task<SiteResponseModel> Get(string id, string[] expand = null)
        {
            var site = await _cipherRepository.GetByIdAsync(new Guid(id), new Guid(_userManager.GetUserId(User)));
            if(site == null || site.Type != Core.Enums.CipherType.Site)
            {
                throw new NotFoundException();
            }

            var response = new SiteResponseModel(site);
            await ExpandAsync(site, response, expand, null);
            return response;
        }

        [HttpGet("")]
        public async Task<ListResponseModel<SiteResponseModel>> Get(string[] expand = null)
        {
            ICollection<Cipher> sites = await _cipherRepository.GetManyByTypeAndUserIdAsync(Core.Enums.CipherType.Site, new Guid(_userManager.GetUserId(User)));
            var responses = sites.Select(s => new SiteResponseModel(s)).ToList();
            await ExpandManyAsync(sites, responses, expand, null);
            return new ListResponseModel<SiteResponseModel>(responses);
        }

        [HttpPost("")]
        public async Task<SiteResponseModel> Post([FromBody]SiteRequestModel model, string[] expand = null)
        {
            var site = model.ToCipher(_userManager.GetUserId(User));
            await _cipherService.SaveAsync(site);

            var response = new SiteResponseModel(site);
            await ExpandAsync(site, response, expand, null);
            return response;
        }

        [HttpPut("{id}")]
        public async Task<SiteResponseModel> Put(string id, [FromBody]SiteRequestModel model, string[] expand = null)
        {
            var site = await _cipherRepository.GetByIdAsync(new Guid(id), new Guid(_userManager.GetUserId(User)));
            if(site == null || site.Type != Core.Enums.CipherType.Site)
            {
                throw new NotFoundException();
            }

            await _cipherService.SaveAsync(model.ToCipher(site));

            var response = new SiteResponseModel(site);
            await ExpandAsync(site, response, expand, null);
            return response;
        }

        [HttpDelete("{id}")]
        public async Task Delete(string id)
        {
            var site = await _cipherRepository.GetByIdAsync(new Guid(id), new Guid(_userManager.GetUserId(User)));
            if(site == null || site.Type != Core.Enums.CipherType.Site)
            {
                throw new NotFoundException();
            }

            await _cipherService.DeleteAsync(site);
        }

        private async Task ExpandAsync(Cipher site, SiteResponseModel response, string[] expand, Cipher folder)
        {
            if(expand == null || expand.Count() == 0)
            {
                return;
            }

            if(expand.Any(e => e.ToLower() == "folder") && site.FolderId.HasValue)
            {
                if(folder == null)
                {
                    folder = await _cipherRepository.GetByIdAsync(site.FolderId.Value);
                }

                response.Folder = new FolderResponseModel(folder);
            }
        }

        private async Task ExpandManyAsync(IEnumerable<Cipher> sites, ICollection<SiteResponseModel> responses, string[] expand, IEnumerable<Cipher> folders)
        {
            if(expand == null || expand.Count() == 0)
            {
                return;
            }

            if(expand.Any(e => e.ToLower() == "folder"))
            {
                if(folders == null)
                {
                    folders = await _cipherRepository.GetManyByTypeAndUserIdAsync(Core.Enums.CipherType.Folder, new Guid(_userManager.GetUserId(User)));
                }

                if(folders != null && folders.Count() > 0)
                {
                    foreach(var response in responses)
                    {
                        var site = sites.SingleOrDefault(s => s.Id.ToString() == response.Id);
                        if(site == null)
                        {
                            continue;
                        }

                        var folder = folders.SingleOrDefault(f => f.Id == site.FolderId);
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
