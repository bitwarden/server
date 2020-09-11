using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Portal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Portal.Controllers
{
    [Authorize]
    public class PoliciesController : Controller
    {
        private readonly IUserService _userService;
        private readonly IOrganizationService _organizationService;
        private readonly IPolicyService _policyService;
        private readonly IPolicyRepository _policyRepository;
        private readonly EnterprisePortalCurrentContext _enterprisePortalCurrentContext;
        private readonly II18nService _i18nService;

        public PoliciesController(
            IUserService userService,
            IOrganizationService organizationService,
            IPolicyService policyService,
            IPolicyRepository policyRepository,
            EnterprisePortalCurrentContext enterprisePortalCurrentContext,
            II18nService i18nService)
        {
            _userService = userService;
            _organizationService = organizationService;
            _policyService = policyService;
            _policyRepository = policyRepository;
            _enterprisePortalCurrentContext = enterprisePortalCurrentContext;
            _i18nService = i18nService;
        }

        public async Task<IActionResult> Index()
        {
            var orgId = _enterprisePortalCurrentContext.SelectedOrganizationId;
            if (orgId == null)
            {
                return Redirect("~/");
            }

            if (!_enterprisePortalCurrentContext.SelectedOrganizationDetails.UsePolicies ||
                !_enterprisePortalCurrentContext.AdminForSelectedOrganization)
            {
                return Redirect("~/");
            }
            
            var policies = await _policyRepository.GetManyByOrganizationIdAsync(orgId.Value);
            return View(new PoliciesModel(policies));
        }
        
        [HttpGet("/edit/{type}")]
        public async Task<IActionResult> Edit(PolicyType type)
        {
            var orgId = _enterprisePortalCurrentContext.SelectedOrganizationId;
            if (orgId == null)
            {
                return Redirect("~");
            }

            if (!_enterprisePortalCurrentContext.SelectedOrganizationDetails.UsePolicies ||
                !_enterprisePortalCurrentContext.AdminForSelectedOrganization)
            {
                return Redirect("~/");
            }

            var policy = await _policyRepository.GetByOrganizationIdTypeAsync(orgId.Value, type);
            return BuildPolicyView(policy, type);
        }
        
        [HttpPost("/edit/{type}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PolicyType type, PolicyEditModel model)
        {
            var orgId = _enterprisePortalCurrentContext.SelectedOrganizationId;
            if (orgId == null)
            {
                return Redirect("~");
            }

            if (!_enterprisePortalCurrentContext.SelectedOrganizationDetails.UsePolicies ||
                !_enterprisePortalCurrentContext.AdminForSelectedOrganization)
            {
                return Redirect("~/");
            }

            var policy = await _policyRepository.GetByOrganizationIdTypeAsync(orgId.Value, type);
            if (!ModelState.IsValid)
            {
                return BuildPolicyView(policy, type);
            }

            if (policy == null)
            {
                policy = model.ToPolicy(type, orgId.Value);
            }
            else
            {
                policy = model.ToPolicy(policy);
            }

            var userId = _userService.GetProperUserId(User);
            await _policyService.SaveAsync(policy, _userService, _organizationService, userId);
            return RedirectToAction("Edit", new { type });
        }

        private IActionResult BuildPolicyView(Policy policy, PolicyType type)
        {
            if (policy == null)
            {
                return View(new PolicyEditModel(type, _i18nService));
            }
            else
            {
                return View(new PolicyEditModel(policy, _i18nService));
            }
        }
    }
}
