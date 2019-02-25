using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Bit.Core.Repositories;
using System.Threading.Tasks;
using Bit.Admin.Models;
using System.Collections.Generic;
using Bit.Core.Models.Table;
using Bit.Core;
using Bit.Core.Utilities;
using Bit.Core.Services;

namespace Bit.Admin.Controllers
{
    [Authorize]
    public class OrganizationsController : Controller
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IPaymentService _paymentService;
        private readonly GlobalSettings _globalSettings;

        public OrganizationsController(
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IPaymentService paymentService,
            GlobalSettings globalSettings)
        {
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _paymentService = paymentService;
            _globalSettings = globalSettings;
        }

        public async Task<IActionResult> Index(string name = null, string userEmail = null, bool? paid = null,
            int page = 1, int count = 25)
        {
            if(page < 1)
            {
                page = 1;
            }

            if(count < 1)
            {
                count = 1;
            }

            var skip = (page - 1) * count;
            var organizations = await _organizationRepository.SearchAsync(name, userEmail, paid, skip, count);
            return View(new OrganizationsModel
            {
                Items = organizations as List<Organization>,
                Name = string.IsNullOrWhiteSpace(name) ? null : name,
                UserEmail = string.IsNullOrWhiteSpace(userEmail) ? null : userEmail,
                Paid = paid,
                Page = page,
                Count = count,
                Action = _globalSettings.SelfHosted ? "View" : "Edit",
                SelfHosted = _globalSettings.SelfHosted
            });
        }

        public async Task<IActionResult> View(Guid id)
        {
            var organization = await _organizationRepository.GetByIdAsync(id);
            if(organization == null)
            {
                return RedirectToAction("Index");
            }

            var users = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(id);
            return View(new OrganizationViewModel(organization, users));
        }

        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task<IActionResult> Edit(Guid id)
        {
            var organization = await _organizationRepository.GetByIdAsync(id);
            if(organization == null)
            {
                return RedirectToAction("Index");
            }

            var users = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(id);
            var billingInfo = await _paymentService.GetBillingAsync(organization);
            return View(new OrganizationEditModel(organization, users, billingInfo, _globalSettings));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task<IActionResult> Edit(Guid id, OrganizationEditModel model)
        {
            var organization = await _organizationRepository.GetByIdAsync(id);
            if(organization == null)
            {
                return RedirectToAction("Index");
            }

            model.ToOrganization(organization);
            await _organizationRepository.ReplaceAsync(organization);
            return RedirectToAction("Edit", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var organization = await _organizationRepository.GetByIdAsync(id);
            if(organization != null)
            {
                await _organizationRepository.DeleteAsync(organization);
            }

            return RedirectToAction("Index");
        }
    }
}
