using System;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers
{
    [Route("providers/{providerId:guid}/organizations")]
    [Authorize("Application")]
    public class ProviderOrganizationsController : Controller
    {
        
        private readonly IProviderOrganizationRepository _providerOrganizationRepository;
        private readonly IProviderService _providerService;
        private readonly IUserService _userService;
        private readonly ICurrentContext _currentContext;

        public ProviderOrganizationsController(
            IProviderOrganizationRepository providerOrganizationRepository,
            IProviderService providerService,
            IUserService userService,
            ICurrentContext currentContext)
        {
            _providerOrganizationRepository = providerOrganizationRepository;
            _providerService = providerService;
            _userService = userService;
            _currentContext = currentContext;
        }
        
        [HttpGet("")]
        public async Task<ListResponseModel<ProviderOrganizationOrganizationDetailsResponseModel>> Get(Guid providerId)
        {
            if (!_currentContext.AccessProviderOrganizations(providerId))
            {
                throw new NotFoundException();
            }

            var providerOrganizations = await _providerOrganizationRepository.GetManyDetailsByProviderAsync(providerId);
            var responses = providerOrganizations.Select(o => new ProviderOrganizationOrganizationDetailsResponseModel(o));
            return new ListResponseModel<ProviderOrganizationOrganizationDetailsResponseModel>(responses);
        }

        [HttpPost("add")]
        public async Task Add(Guid providerId, [FromBody]ProviderOrganizationAddRequestModel model)
        {
            if (!_currentContext.ManageProviderOrganizations(providerId))
            {
                throw new NotFoundException();
            }
            
            var userId = _userService.GetProperUserId(User).Value;

            await _providerService.AddOrganization(providerId, model.OrganizationId, userId, model.Key);
        }

        [HttpPost("")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task<ProviderOrganizationResponseModel> Post(Guid providerId, [FromBody]OrganizationCreateRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var plan = StaticStore.Plans.FirstOrDefault(plan => plan.Type == model.PlanType);
            if (!(plan is {LegacyYear: null}))
            {
                throw new Exception("Invalid plan selected.");
            }

            var organizationSignup = model.ToOrganizationSignup(user);
            var result = await _providerService.CreateOrganizationAsync(providerId, organizationSignup, user);
            return new ProviderOrganizationResponseModel(result);
        }
    }
}
