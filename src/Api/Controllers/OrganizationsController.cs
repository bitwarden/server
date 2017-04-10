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

        public OrganizationsController(
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IOrganizationService organizationService,
            IUserService userService,
            CurrentContext currentContext)
        {
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _organizationService = organizationService;
            _userService = userService;
            _currentContext = currentContext;
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

            var billingInfo = await _organizationService.GetBillingAsync(organization);
            if(billingInfo == null)
            {
                throw new NotFoundException();
            }

            return new OrganizationBillingResponseModel(organization, billingInfo);
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
        public async Task<OrganizationResponseModel> Post([FromBody]OrganizationCreateRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            var organizationSignup = model.ToOrganizationSignup(user);
            var result = await _organizationService.SignUpAsync(organizationSignup);
            return new OrganizationResponseModel(result.Item1);
        }

        [HttpPut("{id}")]
        [HttpPost("{id}")]
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

            await _organizationService.UpdateAsync(model.ToOrganization(organization), true);
            return new OrganizationResponseModel(organization);
        }

        [HttpPut("{id}/payment")]
        [HttpPost("{id}/payment")]
        public async Task PutPayment(string id, [FromBody]OrganizationPaymentRequestModel model)
        {
            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationOwner(orgIdGuid))
            {
                throw new NotFoundException();
            }

            await _organizationService.ReplacePaymentMethodAsync(orgIdGuid, model.PaymentToken);
        }

        [HttpPut("{id}/upgrade")]
        [HttpPost("{id}/upgrade")]
        public async Task PutUpgrade(string id, [FromBody]OrganizationUpgradeRequestModel model)
        {
            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationOwner(orgIdGuid))
            {
                throw new NotFoundException();
            }

            await _organizationService.UpgradePlanAsync(orgIdGuid, model.PlanType, model.AdditionalSeats);
        }

        [HttpPut("{id}/seat")]
        [HttpPost("{id}/seat")]
        public async Task PutSeat(string id, [FromBody]OrganizationSeatRequestModel model)
        {
            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationOwner(orgIdGuid))
            {
                throw new NotFoundException();
            }

            await _organizationService.AdjustSeatsAsync(orgIdGuid, model.SeatAdjustment.Value);
        }

        [HttpPut("{id}/cancel")]
        [HttpPost("{id}/cancel")]
        public async Task PutCancel(string id)
        {
            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationOwner(orgIdGuid))
            {
                throw new NotFoundException();
            }

            await _organizationService.CancelSubscriptionAsync(orgIdGuid, true);
        }

        [HttpPut("{id}/reinstate")]
        [HttpPost("{id}/reinstate")]
        public async Task PutReinstate(string id)
        {
            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationOwner(orgIdGuid))
            {
                throw new NotFoundException();
            }

            await _organizationService.ReinstateSubscriptionAsync(orgIdGuid);
        }

        [HttpDelete("{id}")]
        [HttpPost("{id}/delete")]
        public async Task Delete(string id)
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

            await _organizationRepository.DeleteAsync(organization);
        }
    }
}
