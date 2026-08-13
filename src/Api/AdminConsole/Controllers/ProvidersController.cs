// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.AdminConsole.Authorization;
using Bit.Api.AdminConsole.Authorization.Providers.Requirements;
using Bit.Api.AdminConsole.Models.Request.Providers;
using Bit.Api.AdminConsole.Models.Response.Providers;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Providers.Services;
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
    private readonly GlobalSettings _globalSettings;
    private readonly IProviderBillingService _providerBillingService;
    private readonly ILogger<ProvidersController> _logger;

    public ProvidersController(IUserService userService, IProviderRepository providerRepository,
        IProviderService providerService, GlobalSettings globalSettings,
        IProviderBillingService providerBillingService, ILogger<ProvidersController> logger)
    {
        _userService = userService;
        _providerRepository = providerRepository;
        _providerService = providerService;
        _globalSettings = globalSettings;
        _providerBillingService = providerBillingService;
        _logger = logger;
    }

    [HttpGet("{providerId:guid}")]
    [Authorize<ProviderUserRequirement>]
    public async Task<ProviderResponseModel> Get(Guid providerId)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);
        if (provider == null)
        {
            throw new NotFoundException();
        }

        return new ProviderResponseModel(provider);
    }

    [HttpPut("{providerId:guid}")]
    [Authorize<ProviderAdminRequirement>]
    public async Task<ProviderResponseModel> Put(Guid providerId, [FromBody] ProviderUpdateRequestModel model)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);
        if (provider == null)
        {
            throw new NotFoundException();
        }

        // Capture original values before modifications for Stripe sync
        var originalName = provider.Name;
        var originalBillingEmail = provider.BillingEmail;

        await _providerService.UpdateAsync(model.ToProvider(provider, _globalSettings));

        // Sync name/email changes to Stripe
        if (originalName != provider.Name || originalBillingEmail != provider.BillingEmail)
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

    [HttpPost("{providerId:guid}")]
    [Obsolete("This endpoint is deprecated. Use PUT method instead")]
    [Authorize<ProviderAdminRequirement>]
    public async Task<ProviderResponseModel> PostPut(Guid providerId, [FromBody] ProviderUpdateRequestModel model)
    {
        return await Put(providerId, model);
    }

    [HttpPost("{providerId:guid}/setup")]
    [Authorize<ProviderAdminRequirement>]
    public async Task<ProviderResponseModel> Setup(Guid providerId, [FromBody] ProviderSetupRequestModel model)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);
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

    [HttpPost("{providerId}/delete-recover-token")]
    [AllowAnonymous]
    public async Task PostDeleteRecoverToken(Guid providerId, [FromBody] ProviderVerifyDeleteRecoverRequestModel model)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);
        if (provider == null)
        {
            throw new NotFoundException();
        }
        await _providerService.DeleteAsync(provider, model.Token);
    }

    [HttpDelete("{providerId}")]
    [Authorize<ProviderAdminRequirement>]
    public async Task Delete(Guid providerId)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);
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

    [HttpPost("{providerId}/delete")]
    [Obsolete("This endpoint is deprecated. Use DELETE method instead")]
    [Authorize<ProviderAdminRequirement>]
    public async Task PostDelete(Guid providerId)
    {
        await Delete(providerId);
    }
}
