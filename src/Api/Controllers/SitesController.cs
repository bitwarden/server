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
    [Route("sites")]
    [Authorize("Application")]
    public class SitesController : Controller
    {
        private readonly ISiteRepository _siteRepository;
        private readonly IFolderRepository _folderRepository;
        private readonly UserManager<User> _userManager;

        public SitesController(
            ISiteRepository siteRepository,
            IFolderRepository folderRepository,
            UserManager<User> userManager)
        {
            _siteRepository = siteRepository;
            _folderRepository = folderRepository;
            _userManager = userManager;
        }

        [HttpGet("{id}")]
        public async Task<SiteResponseModel> Get(string id, string[] expand = null)
        {
            var site = await _siteRepository.GetByIdAsync(id, _userManager.GetUserId(User));
            if(site == null)
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
            ICollection<Site> sites = await _siteRepository.GetManyByUserIdAsync(_userManager.GetUserId(User));
            var responses = sites.Select(s => new SiteResponseModel(s)).ToList();
            await ExpandManyAsync(sites, responses, expand, null);
            return new ListResponseModel<SiteResponseModel>(responses);
        }

        [HttpPost("")]
        public async Task<SiteResponseModel> Post([FromBody]SiteRequestModel model, string[] expand = null)
        {
            var site = model.ToSite(_userManager.GetUserId(User));
            await _siteRepository.CreateAsync(site);

            var response = new SiteResponseModel(site);
            await ExpandAsync(site, response, expand, null);
            return response;
        }

        [HttpPut("{id}")]
        public async Task<SiteResponseModel> Put(string id, [FromBody]SiteRequestModel model, string[] expand = null)
        {
            var site = await _siteRepository.GetByIdAsync(id, _userManager.GetUserId(User));
            if(site == null)
            {
                throw new NotFoundException();
            }

            await _siteRepository.ReplaceAsync(model.ToSite(site));

            var response = new SiteResponseModel(site);
            await ExpandAsync(site, response, expand, null);
            return response;
        }

        [HttpDelete("{id}")]
        public async Task Delete(string id)
        {
            var site = await _siteRepository.GetByIdAsync(id, _userManager.GetUserId(User));
            if(site == null)
            {
                throw new NotFoundException();
            }

            await _siteRepository.DeleteAsync(site);
        }

        private async Task ExpandAsync(Site site, SiteResponseModel response, string[] expand, Folder folder)
        {
            if(expand == null || expand.Count() == 0)
            {
                return;
            }

            if(expand.Any(e => e.ToLower() == "folder"))
            {
                if(folder == null)
                {
                    folder = await _folderRepository.GetByIdAsync(site.FolderId);
                }

                response.Folder = new FolderResponseModel(folder);
            }
        }

        private async Task ExpandManyAsync(IEnumerable<Site> sites, ICollection<SiteResponseModel> responses, string[] expand, IEnumerable<Folder> folders)
        {
            if(expand == null || expand.Count() == 0)
            {
                return;
            }

            if(expand.Any(e => e.ToLower() == "folder"))
            {
                if(folders == null)
                {
                    folders = await _folderRepository.GetManyByUserIdAsync(_userManager.GetUserId(User));
                }

                if(folders != null && folders.Count() > 0)
                {
                    foreach(var response in responses)
                    {
                        var site = sites.SingleOrDefault(s => s.Id == response.Id);
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
