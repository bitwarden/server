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
using Bit.Api.Utilities;
using Bit.Core.Models.Business;
using Stripe;
using Microsoft.Extensions.Options;
using Bit.Core.Utilities;

namespace Bit.Api.Controllers
{
    [Route("organizations")]
    [Authorize("Application")]
    public class OrganizationsController : Controller
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IOrganizationService _organizationService;
        private readonly IUserService _userService;
        private readonly CurrentContext _currentContext;
        private readonly GlobalSettings _globalSettings;

        public OrganizationsController(
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IOrganizationService organizationService,
            IUserService userService,
            CurrentContext currentContext,
            GlobalSettings globalSettings)
        {
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _organizationService = organizationService;
            _userService = userService;
            _currentContext = currentContext;
            _globalSettings = globalSettings;
        }

        [HttpGet("{id}")]
        public async Task<OrganizationResponseModel> Get(string id)
        {
            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationOwner(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var organization = await _organizationRepository.GetByIdAsync(orgIdGuid);
            if(organization == null)
            {
                throw new NotFoundException();
            }

            return new OrganizationResponseModel(organization);
        }

        [HttpGet("{id}/billing")]
        public async Task<OrganizationBillingResponseModel> GetBilling(string id)
        {
            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationOwner(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var organization = await _organizationRepository.GetByIdAsync(orgIdGuid);
            if(organization == null)
            {
                throw new NotFoundException();
            }

            if(!_globalSettings.SelfHosted && organization.Gateway != null)
            {
                var paymentService = new StripePaymentService(_globalSettings);
                var billingInfo = await paymentService.GetBillingAsync(organization);
                if(billingInfo == null)
                {
                    throw new NotFoundException();
                }
                return new OrganizationBillingResponseModel(organization, billingInfo);
            }
            else
            {
                return new OrganizationBillingResponseModel(organization);
            }
        }

        [HttpGet("{id}/billing-invoice/{invoiceId}")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task<IActionResult> GetBillingInvoice(string id, string invoiceId)
        {
            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationOwner(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var organization = await _organizationRepository.GetByIdAsync(orgIdGuid);
            if(organization == null)
            {
                throw new NotFoundException();
            }

            try
            {
                var invoice = await new StripeInvoiceService().GetAsync(invoiceId);
                if(invoice != null && invoice.CustomerId == organization.GatewayCustomerId &&
                    !string.IsNullOrWhiteSpace(invoice.HostedInvoiceUrl))
                {
                    return new RedirectResult(invoice.HostedInvoiceUrl);
                }
            }
            catch(StripeException) { }

            throw new NotFoundException();
        }

        [HttpGet("{id}/license")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task<OrganizationLicense> GetLicense(string id, [FromQuery]Guid installationId)
        {
            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationOwner(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var license = await _organizationService.GenerateLicenseAsync(orgIdGuid, installationId);
            if(license == null)
            {
                throw new NotFoundException();
            }

            return license;
        }

        [HttpGet("")]
        public async Task<ListResponseModel<OrganizationResponseModel>> GetUser()
        {
            var userId = _userService.GetProperUserId(User).Value;
            var organizations = await _organizationRepository.GetManyByUserIdAsync(userId);
            var responses = organizations.Select(o => new OrganizationResponseModel(o));
            return new ListResponseModel<OrganizationResponseModel>(responses);
        }

        [HttpPost("")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task<OrganizationResponseModel> Post([FromBody]OrganizationCreateRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if(user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var organizationSignup = model.ToOrganizationSignup(user);
            var result = await _organizationService.SignUpAsync(organizationSignup);
            return new OrganizationResponseModel(result.Item1);
        }

        [HttpPost("license")]
        [SelfHosted(SelfHostedOnly = true)]
        public async Task<OrganizationResponseModel> PostLicense(OrganizationCreateLicenseRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if(user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var license = await ApiHelpers.ReadJsonFileFromBody<OrganizationLicense>(HttpContext, model.License);
            if(license == null)
            {
                throw new BadRequestException("Invalid license");
            }

            var result = await _organizationService.SignUpAsync(license, user, model.Key, model.CollectionName);
            return new OrganizationResponseModel(result.Item1);
        }

        [HttpPut("{id}")]
        [HttpPost("{id}")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task<OrganizationResponseModel> Put(string id, [FromBody]OrganizationUpdateRequestModel model)
        {
            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationOwner(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var organization = await _organizationRepository.GetByIdAsync(orgIdGuid);
            if(organization == null)
            {
                throw new NotFoundException();
            }

            var updatebilling = model.BusinessName != organization.BusinessName ||
                model.BillingEmail != organization.BillingEmail;

            await _organizationService.UpdateAsync(model.ToOrganization(organization), updatebilling);
            return new OrganizationResponseModel(organization);
        }

        [HttpPost("{id}/payment")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task PostPayment(string id, [FromBody]PaymentRequestModel model)
        {
            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationOwner(orgIdGuid))
            {
                throw new NotFoundException();
            }

            await _organizationService.ReplacePaymentMethodAsync(orgIdGuid, model.PaymentToken);
        }

        [HttpPost("{id}/upgrade")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task PostUpgrade(string id, [FromBody]OrganizationUpgradeRequestModel model)
        {
            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationOwner(orgIdGuid))
            {
                throw new NotFoundException();
            }

            await _organizationService.UpgradePlanAsync(orgIdGuid, model.PlanType, model.AdditionalSeats);
        }

        [HttpPost("{id}/seat")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task PostSeat(string id, [FromBody]OrganizationSeatRequestModel model)
        {
            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationOwner(orgIdGuid))
            {
                throw new NotFoundException();
            }

            await _organizationService.AdjustSeatsAsync(orgIdGuid, model.SeatAdjustment.Value);
        }

        [HttpPost("{id}/storage")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task PostStorage(string id, [FromBody]StorageRequestModel model)
        {
            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationOwner(orgIdGuid))
            {
                throw new NotFoundException();
            }

            await _organizationService.AdjustStorageAsync(orgIdGuid, model.StorageGbAdjustment.Value);
        }

        [HttpPost("{id}/verify-bank")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task PostVerifyBank(string id, [FromBody]OrganizationVerifyBankRequestModel model)
        {
            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationOwner(orgIdGuid))
            {
                throw new NotFoundException();
            }

            await _organizationService.VerifyBankAsync(orgIdGuid, model.Amount1.Value, model.Amount2.Value);
        }

        [HttpPost("{id}/cancel")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task PostCancel(string id)
        {
            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationOwner(orgIdGuid))
            {
                throw new NotFoundException();
            }

            await _organizationService.CancelSubscriptionAsync(orgIdGuid);
        }

        [HttpPost("{id}/reinstate")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task PostReinstate(string id)
        {
            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationOwner(orgIdGuid))
            {
                throw new NotFoundException();
            }

            await _organizationService.ReinstateSubscriptionAsync(orgIdGuid);
        }

        [HttpPost("{id}/leave")]
        public async Task Leave(string id)
        {
            var orgGuidId = new Guid(id);
            if(!_currentContext.OrganizationUser(orgGuidId))
            {
                throw new NotFoundException();
            }

            var userId = _userService.GetProperUserId(User);
            await _organizationService.DeleteUserAsync(orgGuidId, userId.Value);
        }

        [HttpDelete("{id}")]
        [HttpPost("{id}/delete")]
        public async Task Delete(string id, [FromBody]OrganizationDeleteRequestModel model)
        {
            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationOwner(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var organization = await _organizationRepository.GetByIdAsync(orgIdGuid);
            if(organization == null)
            {
                throw new NotFoundException();
            }

            var user = await _userService.GetUserByPrincipalAsync(User);
            if(user == null)
            {
                throw new UnauthorizedAccessException();
            }

            if(!await _userService.CheckPasswordAsync(user, model.MasterPasswordHash))
            {
                await Task.Delay(2000);
                throw new BadRequestException("MasterPasswordHash", "Invalid password.");
            }
            else
            {
                await _organizationService.DeleteAsync(organization);
            }
        }

        [HttpPost("{id}/license")]
        [SelfHosted(SelfHostedOnly = true)]
        public async Task PostLicense(string id, LicenseRequestModel model)
        {
            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationOwner(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var license = await ApiHelpers.ReadJsonFileFromBody<OrganizationLicense>(HttpContext, model.License);
            if(license == null)
            {
                throw new BadRequestException("Invalid license");
            }

            await _organizationService.UpdateLicenseAsync(new Guid(id), license);
        }

        [HttpPost("{id}/import")]
        public async Task Import(string id, [FromBody]ImportOrganizationUsersRequestModel model)
        {
            if(!_globalSettings.SelfHosted && (model.Groups.Count() > 200 || model.Users.Count() > 1000))
            {
                throw new BadRequestException("You cannot import this much data at once.");
            }

            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationAdmin(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var userId = _userService.GetProperUserId(User);
            await _organizationService.ImportAsync(
                orgIdGuid,
                userId.Value,
                model.Groups.Select(g => g.ToImportedGroup(orgIdGuid)),
                model.Users.Where(u => !u.Deleted).Select(u => u.ToImportedOrganizationUser()),
                model.Users.Where(u => u.Deleted).Select(u => u.ExternalId));
        }
    }
}
