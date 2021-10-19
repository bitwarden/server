using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Bit.Core.Repositories;
using System.Threading.Tasks;
using Bit.Admin.Models;
using System.Collections.Generic;
using Bit.Core.Models.Table;
using Bit.Core.Utilities;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Models.Business;
using Bit.Core.Enums;

namespace Bit.Admin.Controllers
{
    [Authorize]
    public class OrganizationsController : Controller
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly ICipherRepository _cipherRepository;
        private readonly ICollectionRepository _collectionRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IPolicyRepository _policyRepository;
        private readonly IPaymentService _paymentService;
        private readonly IApplicationCacheService _applicationCacheService;
        private readonly GlobalSettings _globalSettings;
        private readonly IReferenceEventService _referenceEventService;
        private readonly IUserService _userService;

        public OrganizationsController(
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            ICipherRepository cipherRepository,
            ICollectionRepository collectionRepository,
            IGroupRepository groupRepository,
            IPolicyRepository policyRepository,
            IPaymentService paymentService,
            IApplicationCacheService applicationCacheService,
            GlobalSettings globalSettings,
            IReferenceEventService referenceEventService,
            IUserService userService)
        {
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _cipherRepository = cipherRepository;
            _collectionRepository = collectionRepository;
            _groupRepository = groupRepository;
            _policyRepository = policyRepository;
            _paymentService = paymentService;
            _applicationCacheService = applicationCacheService;
            _globalSettings = globalSettings;
            _referenceEventService = referenceEventService;
            _userService = userService;
        }

        public async Task<IActionResult> Index(string name = null, string userEmail = null, bool? paid = null,
            int page = 1, int count = 25)
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
            if (organization == null)
            {
                return RedirectToAction("Index");
            }

            var ciphers = await _cipherRepository.GetManyByOrganizationIdAsync(id);
            var collections = await _collectionRepository.GetManyByOrganizationIdAsync(id);
            IEnumerable<Group> groups = null;
            if (organization.UseGroups)
            {
                groups = await _groupRepository.GetManyByOrganizationIdAsync(id);
            }
            IEnumerable<Policy> policies = null;
            if (organization.UsePolicies)
            {
                policies = await _policyRepository.GetManyByOrganizationIdAsync(id);
            }
            var users = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(id);
            return View(new OrganizationViewModel(organization, users, ciphers, collections, groups, policies));
        }

        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task<IActionResult> Edit(Guid id)
        {
            var organization = await _organizationRepository.GetByIdAsync(id);
            if (organization == null)
            {
                return RedirectToAction("Index");
            }

            var ciphers = await _cipherRepository.GetManyByOrganizationIdAsync(id);
            var collections = await _collectionRepository.GetManyByOrganizationIdAsync(id);
            IEnumerable<Group> groups = null;
            if (organization.UseGroups)
            {
                groups = await _groupRepository.GetManyByOrganizationIdAsync(id);
            }
            IEnumerable<Policy> policies = null;
            if (organization.UsePolicies)
            {
                policies = await _policyRepository.GetManyByOrganizationIdAsync(id);
            }
            var users = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(id);
            var billingInfo = await _paymentService.GetBillingAsync(organization);
            return View(new OrganizationEditModel(organization, users, ciphers, collections, groups, policies,
                billingInfo, _globalSettings));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task<IActionResult> Edit(Guid id, OrganizationEditModel model)
        {
            var organization = await _organizationRepository.GetByIdAsync(id);
            model.ToOrganization(organization);
            await _organizationRepository.ReplaceAsync(organization);
            await _applicationCacheService.UpsertOrganizationAbilityAsync(organization);
            await _referenceEventService.RaiseEventAsync(new ReferenceEvent(ReferenceEventType.OrganizationEditedByAdmin, organization) {
                EventRaisedByUser = _userService.GetUserName(User),
                SalesAssistedTrialStarted = model.SalesAssistedTrialStarted,
            });
            return RedirectToAction("Edit", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var organization = await _organizationRepository.GetByIdAsync(id);
            if (organization != null)
            {
                await _organizationRepository.DeleteAsync(organization);
                await _applicationCacheService.DeleteOrganizationAbilityAsync(organization.Id);
            }

            return RedirectToAction("Index");
        }
    }
}
