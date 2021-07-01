using System;
using System.Threading.Tasks;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers
{
    [Route("providers")]
    [Authorize("Application")]
    public class ProvidersController : Controller
    {
        private readonly IUserService _userService;
        private readonly IProviderRepository _providerRepository;
        private readonly IProviderService _providerService;
        private readonly ICurrentContext _currentContext;

        public ProvidersController(IUserService userService, IProviderRepository providerRepository,
            IProviderService providerService, ICurrentContext currentContext)
        {
            _userService = userService;
            _providerRepository = providerRepository;
            _providerService = providerService;
            _currentContext = currentContext;
        }
        
        [HttpGet("{id:guid}")]
        public async Task<ProviderResponseModel> Get(Guid id)
        {
            if (!_currentContext.ProviderUser(id))
            {
                throw new NotFoundException();
            }

            var provider = await _providerRepository.GetByIdAsync(id);
            if (provider == null)
            {
                throw new NotFoundException();
            }

            return new ProviderResponseModel(provider);
        }
        
        [HttpPost("{id:guid}/setup")]
        public async Task<ProviderResponseModel> Setup(Guid id, [FromBody]ProviderSetupRequestModel model)
        {
            if (!_currentContext.ProviderProviderAdmin(id))
            {
                throw new NotFoundException();
            }

            var provider = await _providerRepository.GetByIdAsync(id);
            if (provider == null)
            {
                throw new NotFoundException();
            }
            
            var userId = _userService.GetProperUserId(User).Value;
            
            var response =
                await _providerService.CompleteSetupAsync(model.ToProvider(provider), userId, model.Token, model.Key);

            return new ProviderResponseModel(response);
        }
    }
}
