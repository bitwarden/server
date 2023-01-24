using Bit.Api.Models.Response;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.AccessTokens.Interfaces;
using Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.SecretsManager.Controllers;

[SecretsManager]
[Route("service-accounts")]
public class ServiceAccountsController : Controller
{
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly ICreateAccessTokenCommand _createAccessTokenCommand;
    private readonly ICreateServiceAccountCommand _createServiceAccountCommand;
    private readonly ICurrentContext _currentContext;
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly IUpdateServiceAccountCommand _updateServiceAccountCommand;
    private readonly IUserService _userService;

    public ServiceAccountsController(
        IUserService userService,
        IServiceAccountRepository serviceAccountRepository,
        ICreateAccessTokenCommand createAccessTokenCommand,
        IApiKeyRepository apiKeyRepository, ICreateServiceAccountCommand createServiceAccountCommand,
        IUpdateServiceAccountCommand updateServiceAccountCommand,
        ICurrentContext currentContext)
    {
        _userService = userService;
        _serviceAccountRepository = serviceAccountRepository;
        _apiKeyRepository = apiKeyRepository;
        _createServiceAccountCommand = createServiceAccountCommand;
        _updateServiceAccountCommand = updateServiceAccountCommand;
        _createAccessTokenCommand = createAccessTokenCommand;
        _currentContext = currentContext;
    }

    [HttpGet("/organizations/{organizationId}/service-accounts")]
    public async Task<ListResponseModel<ServiceAccountResponseModel>> GetServiceAccountsByOrganizationAsync(
        [FromRoute] Guid organizationId)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var serviceAccounts =
            await _serviceAccountRepository.GetManyByOrganizationIdAsync(organizationId, userId, accessClient);

        var responses = serviceAccounts.Select(serviceAccount => new ServiceAccountResponseModel(serviceAccount));
        return new ListResponseModel<ServiceAccountResponseModel>(responses);
    }

    [HttpPost("/organizations/{organizationId}/service-accounts")]
    public async Task<ServiceAccountResponseModel> CreateServiceAccountAsync([FromRoute] Guid organizationId,
        [FromBody] ServiceAccountCreateRequestModel createRequest)
    {
        if (!await _currentContext.OrganizationUser(organizationId))
        {
            throw new NotFoundException();
        }

        var result = await _createServiceAccountCommand.CreateAsync(createRequest.ToServiceAccount(organizationId));
        return new ServiceAccountResponseModel(result);
    }

    [HttpPut("{id}")]
    public async Task<ServiceAccountResponseModel> UpdateServiceAccountAsync([FromRoute] Guid id,
        [FromBody] ServiceAccountUpdateRequestModel updateRequest)
    {
        var userId = _userService.GetProperUserId(User).Value;

        var result = await _updateServiceAccountCommand.UpdateAsync(updateRequest.ToServiceAccount(id), userId);
        return new ServiceAccountResponseModel(result);
    }

    [HttpGet("{id}/access-tokens")]
    public async Task<ListResponseModel<AccessTokenResponseModel>> GetAccessTokens([FromRoute] Guid id)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(id);
        if (serviceAccount == null)
        {
            throw new NotFoundException();
        }

        var orgAdmin = await _currentContext.OrganizationAdmin(serviceAccount.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var hasAccess = accessClient switch
        {
            AccessClientType.NoAccessCheck => true,
            AccessClientType.User => await _serviceAccountRepository.UserHasReadAccessToServiceAccount(id, userId),
            _ => false,
        };
        if (!hasAccess)
        {
            throw new NotFoundException();
        }

        var accessTokens = await _apiKeyRepository.GetManyByServiceAccountIdAsync(id);
        var responses = accessTokens.Select(token => new AccessTokenResponseModel(token));
        return new ListResponseModel<AccessTokenResponseModel>(responses);
    }

    [HttpPost("{id}/access-tokens")]
    public async Task<AccessTokenCreationResponseModel> CreateAccessTokenAsync([FromRoute] Guid id,
        [FromBody] AccessTokenCreateRequestModel request)
    {
        var userId = _userService.GetProperUserId(User).Value;

        var result = await _createAccessTokenCommand.CreateAsync(request.ToApiKey(id), userId);
        return new AccessTokenCreationResponseModel(result);
    }
}
