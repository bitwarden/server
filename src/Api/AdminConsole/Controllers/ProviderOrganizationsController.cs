using Bit.Api.AdminConsole.Models.Request.Providers;
using Bit.Api.AdminConsole.Models.Response.Providers;
using Bit.Api.Models.Response;
using Bit.Core.AdminConsole.Providers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

[Route("providers/{providerId:guid}/organizations")]
[Authorize("Application")]
public class ProviderOrganizationsController : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProviderOrganizationRepository _providerOrganizationRepository;
    private readonly IProviderRepository _providerRepository;
    private readonly IProviderService _providerService;
    private readonly IRemoveOrganizationFromProviderCommand _removeOrganizationFromProviderCommand;
    private readonly IUserService _userService;

    public ProviderOrganizationsController(
        ICurrentContext currentContext,
        IOrganizationRepository organizationRepository,
        IProviderOrganizationRepository providerOrganizationRepository,
        IProviderRepository providerRepository,
        IProviderService providerService,
        IRemoveOrganizationFromProviderCommand removeOrganizationFromProviderCommand,
        IUserService userService)
    {
        _currentContext = currentContext;
        _organizationRepository = organizationRepository;
        _providerOrganizationRepository = providerOrganizationRepository;
        _providerRepository = providerRepository;
        _providerService = providerService;
        _removeOrganizationFromProviderCommand = removeOrganizationFromProviderCommand;
        _userService = userService;
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
    public async Task Add(Guid providerId, [FromBody] ProviderOrganizationAddRequestModel model)
    {
        if (!_currentContext.ManageProviderOrganizations(providerId))
        {
            throw new NotFoundException();
        }

        await _providerService.AddOrganization(providerId, model.OrganizationId, model.Key);
    }

    [HttpPost("")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<ProviderOrganizationResponseModel> Post(Guid providerId, [FromBody] ProviderOrganizationCreateRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        if (!_currentContext.ManageProviderOrganizations(providerId))
        {
            throw new NotFoundException();
        }

        var organizationSignup = model.OrganizationCreateRequest.ToOrganizationSignup(user);
        organizationSignup.IsFromProvider = true;
        var result = await _providerService.CreateOrganizationAsync(providerId, organizationSignup, model.ClientOwnerEmail, user);
        return new ProviderOrganizationResponseModel(result);
    }

    [HttpDelete("{id:guid}")]
    [HttpPost("{id:guid}/delete")]
    public async Task Delete(Guid providerId, Guid id)
    {
        if (!_currentContext.ManageProviderOrganizations(providerId))
        {
            throw new NotFoundException();
        }

        var provider = await _providerRepository.GetByIdAsync(providerId);

        var providerOrganization = await _providerOrganizationRepository.GetByIdAsync(id);

        var organization = await _organizationRepository.GetByIdAsync(providerOrganization.OrganizationId);

        await _removeOrganizationFromProviderCommand.RemoveOrganizationFromProvider(
            provider,
            providerOrganization,
            organization);
    }
}
