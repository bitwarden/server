using Bit.Api.AdminConsole.Models.Request.Providers;
using Bit.Api.AdminConsole.Models.Response.Providers;
using Bit.Core;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Commands;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

[Route("providers")]
[Authorize("Application")]
public class ProvidersController : Controller
{
    private readonly IUserService _userService;
    private readonly IProviderRepository _providerRepository;
    private readonly IProviderService _providerService;
    private readonly ICurrentContext _currentContext;
    private readonly GlobalSettings _globalSettings;
    private readonly IFeatureService _featureService;
    private readonly IStartSubscriptionCommand _startSubscriptionCommand;
    private readonly ILogger<ProvidersController> _logger;

    public ProvidersController(IUserService userService, IProviderRepository providerRepository,
        IProviderService providerService, ICurrentContext currentContext, GlobalSettings globalSettings,
        IFeatureService featureService, IStartSubscriptionCommand startSubscriptionCommand,
        ILogger<ProvidersController> logger)
    {
        _userService = userService;
        _providerRepository = providerRepository;
        _providerService = providerService;
        _currentContext = currentContext;
        _globalSettings = globalSettings;
        _featureService = featureService;
        _startSubscriptionCommand = startSubscriptionCommand;
        _logger = logger;
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

    [HttpPut("{id:guid}")]
    [HttpPost("{id:guid}")]
    public async Task<ProviderResponseModel> Put(Guid id, [FromBody] ProviderUpdateRequestModel model)
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

        await _providerService.UpdateAsync(model.ToProvider(provider, _globalSettings));
        return new ProviderResponseModel(provider);
    }

    [HttpPost("{id:guid}/setup")]
    public async Task<ProviderResponseModel> Setup(Guid id, [FromBody] ProviderSetupRequestModel model)
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

        if (_featureService.IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling))
        {
            var taxInfo = new TaxInfo
            {
                BillingAddressCountry = model.TaxInfo.Country,
                BillingAddressPostalCode = model.TaxInfo.PostalCode,
                TaxIdNumber = model.TaxInfo.TaxId,
                BillingAddressLine1 = model.TaxInfo.Line1,
                BillingAddressLine2 = model.TaxInfo.Line2,
                BillingAddressCity = model.TaxInfo.City,
                BillingAddressState = model.TaxInfo.State
            };

            try
            {
                await _startSubscriptionCommand.StartSubscription(provider, taxInfo);
            }
            catch
            {
                // We don't want to trap the user on the setup page, so we'll let this go through but the provider will be in an un-billable state.
                _logger.LogError("Failed to create subscription for provider with ID {ID} during setup", provider.Id);
            }
        }

        return new ProviderResponseModel(response);
    }

    [HttpPost("{id}/delete-recover-token")]
    [AllowAnonymous]
    public async Task PostDeleteRecoverToken(Guid id, [FromBody] ProviderVerifyDeleteRecoverRequestModel model)
    {
        var provider = await _providerRepository.GetByIdAsync(id);
        if (provider == null)
        {
            throw new NotFoundException();
        }
        await _providerService.DeleteAsync(provider, model.Token);
    }

    [HttpDelete("{id}")]
    [HttpPost("{id}/delete")]
    public async Task Delete(string id)
    {
        var providerIdGuid = new Guid(id);
        if (!_currentContext.ProviderProviderAdmin(providerIdGuid))
        {
            throw new NotFoundException();
        }

        var provider = await _providerRepository.GetByIdAsync(providerIdGuid);
        if (provider == null)
        {
            throw new NotFoundException();
        }

        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        await _providerService.DeleteAsync(provider);
    }
}
