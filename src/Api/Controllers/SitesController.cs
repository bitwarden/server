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
    [Route("sites")]
    [Authorize("Application")]
    public class SitesController : Controller
    {
        private readonly ISiteRepository _siteRepository;
        private readonly IFolderRepository _folderRepository;

        public SitesController(
            ISiteRepository siteRepository,
            IFolderRepository folderRepository)
        {
            _siteRepository = siteRepository;
            _folderRepository = folderRepository;
        }

        [HttpGet("{id}")]
        public async Task<SiteResponseModel> Get(string id, string[] expand = null)
        {
            var site = await _siteRepository.GetByIdAsync(id, User.GetUserId());
            if(site == null)
            {
                throw new NotFoundException();
            }

            var response = new SiteResponseModel(site);
            await ExpandAsync(site, response, expand, null);
            return response;
        }

        [HttpGet("")]
        public async Task<ListResponseModel<SiteResponseModel>> Get(bool dirty = false, string[] expand = null)
        {
            var sites = await _siteRepository.GetManyByUserIdAsync(User.GetUserId(), dirty);

            var responses = sites.Select(s => new SiteResponseModel(s));
            await ExpandManyAsync(sites, responses, expand, null);
            return new ListResponseModel<SiteResponseModel>(responses);
        }

        [HttpPost("")]
        public async Task<SiteResponseModel> Post([FromBody]SiteRequestModel model, string[] expand = null)
        {
            var site = model.ToSite(User.GetUserId());
            await _siteRepository.CreateAsync(site);

            var response = new SiteResponseModel(site);
            await ExpandAsync(site, response, expand, null);
            return response;
        }

        [HttpPut("{id}")]
        public async Task<SiteResponseModel> Put(string id, [FromBody]SiteRequestModel model, string[] expand = null)
        {
            var site = await _siteRepository.GetByIdAsync(id, User.GetUserId());
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
            var site = await _siteRepository.GetByIdAsync(id, User.GetUserId());
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

        private async Task ExpandManyAsync(IEnumerable<Site> sites, IEnumerable<SiteResponseModel> responses, string[] expand, IEnumerable<Folder> folders)
        {
            if(expand == null || expand.Count() == 0)
            {
                return;
            }

            if(expand.Any(e => e.ToLower() == "folder"))
            {
                if(folders == null)
                {
                    folders = await _folderRepository.GetManyByUserIdAsync(User.GetUserId());
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
