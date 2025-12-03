// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.AdminConsole.Models.Request.Providers;
using Bit.Api.AdminConsole.Models.Response.Providers;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Providers.Services;
using Bit.Core.Context;
using Bit.Core.Exceptions;
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
    private readonly IProviderBillingService _providerBillingService;
    private readonly ILogger<ProvidersController> _logger;

    public ProvidersController(IUserService userService, IProviderRepository providerRepository,
        IProviderService providerService, ICurrentContext currentContext, GlobalSettings globalSettings,
        IProviderBillingService providerBillingService, ILogger<ProvidersController> logger)
    {
        _userService = userService;
        _providerRepository = providerRepository;
        _providerService = providerService;
        _currentContext = currentContext;
        _globalSettings = globalSettings;
        _providerBillingService = providerBillingService;
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

        // Capture original values before modifications for Stripe sync
        var originalName = provider.Name;
        var originalBillingEmail = provider.BillingEmail;
        var hasGatewayCustomerId = !string.IsNullOrWhiteSpace(provider.GatewayCustomerId);

        await _providerService.UpdateAsync(model.ToProvider(provider, _globalSettings));

        // Sync name/email changes to Stripe
        if (hasGatewayCustomerId &&
            (originalName != provider.Name || originalBillingEmail != provider.BillingEmail))
        {
            try
            {
                await _providerBillingService.UpdateProviderNameAndEmail(provider);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to update Stripe customer for provider {ProviderId}. Database was updated successfully.",
                    provider.Id);
            }
        }

        return new ProviderResponseModel(provider);
    }

    [HttpPost("{id:guid}")]
    [Obsolete("This endpoint is deprecated. Use PUT method instead")]
    public async Task<ProviderResponseModel> PostPut(Guid id, [FromBody] ProviderUpdateRequestModel model)
    {
        return await Put(id, model);
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

        var paymentMethod = model.PaymentMethod.ToDomain();
        var billingAddress = model.BillingAddress.ToDomain();

        var response =
            await _providerService.CompleteSetupAsync(model.ToProvider(provider), userId, model.Token, model.Key,
                paymentMethod, billingAddress);

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
    public async Task Delete(Guid id)
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

        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        await _providerService.DeleteAsync(provider);
    }

    [HttpPost("{id}/delete")]
    [Obsolete("This endpoint is deprecated. Use DELETE method instead")]
    public async Task PostDelete(Guid id)
    {
        await Delete(id);
    }
}
