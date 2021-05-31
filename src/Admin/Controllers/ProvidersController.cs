using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Admin.Models;
using Bit.Core.Models.Table.Provider;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Admin.Controllers
{
    [Authorize]
    public class ProvidersController : Controller
    {
        private readonly IProviderRepository _providerRepository;
        private readonly IProviderUserRepository _providerUserRepository;
        private readonly GlobalSettings _globalSettings;
        private readonly IProviderService _providerService;

        public ProvidersController(IProviderRepository providerRepository, IProviderUserRepository providerUserRepository,
            IProviderService providerService, GlobalSettings globalSettings)
        {
            _providerRepository = providerRepository;
            _providerUserRepository = providerUserRepository;
            _providerService = providerService;
            _globalSettings = globalSettings;
        }
        
        public async Task<IActionResult> Index(string name = null, string userEmail = null, int page = 1, int count = 25)
        {
            if (page < 1)
            {
                page = 1;
            }

            if (count < 1)
            {
                count = 1;
            }

            var skip = (page - 1) * count;
            var providers = await _providerRepository.SearchAsync(name, userEmail, skip, count);
            return View(new ProvidersModel
            {
                Items = providers as List<Provider>,
                Name = string.IsNullOrWhiteSpace(name) ? null : name,
                UserEmail = string.IsNullOrWhiteSpace(userEmail) ? null : userEmail,
                Page = page,
                Count = count,
                Action = _globalSettings.SelfHosted ? "View" : "Edit",
                SelfHosted = _globalSettings.SelfHosted
            });
        }
        
        public IActionResult Create(Guid? userId = null)
        {
            return View(new CreateProviderModel
            {
                UserId = userId
            });
        }
        
        [HttpPost]
        public async Task<IActionResult> Create(CreateProviderModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            await _providerService.CreateAsync(model.UserId.Value);

            return RedirectToAction("Index");
        }
        
        public async Task<IActionResult> View(Guid id)
        {
            var provider = await _providerRepository.GetByIdAsync(id);
            if (provider == null)
            {
                return RedirectToAction("Index");
            }

            var users = await _providerUserRepository.GetManyByProviderAsync(id);
            return View(new ProviderViewModel(provider, users));
        }
        
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task<IActionResult> Edit(Guid id)
        {
            var provider = await _providerRepository.GetByIdAsync(id);
            if (provider == null)
            {
                return RedirectToAction("Index");
            }

            var users = await _providerUserRepository.GetManyByProviderAsync(id);
            return View(new ProviderEditModel(provider, users));
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var provider = await _providerRepository.GetByIdAsync(id);
            if (provider != null)
            {
                await _providerRepository.DeleteAsync(provider);
            }

            return RedirectToAction("Index");
        }
    }
}
