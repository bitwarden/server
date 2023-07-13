using Bit.Api.Models.Response;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Commands.AccessTokens.Interfaces;
using Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;
using Bit.Core.SecretsManager.Queries.ServiceAccounts.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.SecretsManager.Controllers;

[Authorize("secrets")]
[SelfHosted(NotSelfHostedOnly = true)]
[Route("service-accounts")]
public class ServiceAccountsController : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly IUserService _userService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IServiceAccountSecretsDetailsQuery _serviceAccountSecretsDetailsQuery;
    private readonly ICreateAccessTokenCommand _createAccessTokenCommand;
    private readonly ICreateServiceAccountCommand _createServiceAccountCommand;
    private readonly IUpdateServiceAccountCommand _updateServiceAccountCommand;
    private readonly IDeleteServiceAccountsCommand _deleteServiceAccountsCommand;
    private readonly IRevokeAccessTokensCommand _revokeAccessTokensCommand;

    public ServiceAccountsController(
        ICurrentContext currentContext,
        IUserService userService,
        IAuthorizationService authorizationService,
        IServiceAccountRepository serviceAccountRepository,
        IApiKeyRepository apiKeyRepository,
        IServiceAccountSecretsDetailsQuery serviceAccountSecretsDetailsQuery,
        ICreateAccessTokenCommand createAccessTokenCommand,
        ICreateServiceAccountCommand createServiceAccountCommand,
        IUpdateServiceAccountCommand updateServiceAccountCommand,
        IDeleteServiceAccountsCommand deleteServiceAccountsCommand,
        IRevokeAccessTokensCommand revokeAccessTokensCommand)
    {
        _currentContext = currentContext;
        _userService = userService;
        _authorizationService = authorizationService;
        _serviceAccountRepository = serviceAccountRepository;
        _apiKeyRepository = apiKeyRepository;
        _serviceAccountSecretsDetailsQuery = serviceAccountSecretsDetailsQuery;
        _createServiceAccountCommand = createServiceAccountCommand;
        _updateServiceAccountCommand = updateServiceAccountCommand;
        _deleteServiceAccountsCommand = deleteServiceAccountsCommand;
        _revokeAccessTokensCommand = revokeAccessTokensCommand;
        _createAccessTokenCommand = createAccessTokenCommand;
    }

    [HttpGet("/organizations/{organizationId}/service-accounts")]
    public async Task<ListResponseModel<ServiceAccountSecretsDetailsResponseModel>> ListByOrganizationAsync(
        [FromRoute] Guid organizationId, [FromQuery] bool includeAccessToSecrets = false)
    {
        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var results =
            await _serviceAccountSecretsDetailsQuery.GetManyByOrganizationIdAsync(organizationId, userId, accessClient,
                includeAccessToSecrets);
        var responses = results.Select(r => new ServiceAccountSecretsDetailsResponseModel(r));
        return new ListResponseModel<ServiceAccountSecretsDetailsResponseModel>(responses);
    }

    [HttpGet("{id}")]
    public async Task<ServiceAccountResponseModel> GetByServiceAccountIdAsync(
        [FromRoute] Guid id)
    {
        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(id);
        var authorizationResult =
            await _authorizationService.AuthorizeAsync(User, serviceAccount, ServiceAccountOperations.Read);

        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        return new ServiceAccountResponseModel(serviceAccount);
    }

    [HttpPost("/organizations/{organizationId}/service-accounts")]
    public async Task<ServiceAccountResponseModel> CreateAsync([FromRoute] Guid organizationId,
        [FromBody] ServiceAccountCreateRequestModel createRequest)
    {
        var serviceAccount = createRequest.ToServiceAccount(organizationId);
        var authorizationResult =
            await _authorizationService.AuthorizeAsync(User, serviceAccount, ServiceAccountOperations.Create);

        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        var result =
            await _createServiceAccountCommand.CreateAsync(createRequest.ToServiceAccount(organizationId), userId);
        return new ServiceAccountResponseModel(result);
    }

    [HttpPut("{id}")]
    public async Task<ServiceAccountResponseModel> UpdateAsync([FromRoute] Guid id,
        [FromBody] ServiceAccountUpdateRequestModel updateRequest)
    {
        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(id);
        var authorizationResult =
            await _authorizationService.AuthorizeAsync(User, serviceAccount, ServiceAccountOperations.Update);

        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        var result = await _updateServiceAccountCommand.UpdateAsync(updateRequest.ToServiceAccount(id));
        return new ServiceAccountResponseModel(result);
    }

    [HttpPost("delete")]
    public async Task<ListResponseModel<BulkDeleteResponseModel>> BulkDeleteAsync([FromBody] List<Guid> ids)
    {
        var serviceAccounts = (await _serviceAccountRepository.GetManyByIds(ids)).ToList();
        if (!serviceAccounts.Any() || serviceAccounts.Count != ids.Count)
        {
            throw new NotFoundException();
        }

        // Ensure all service accounts belong to the same organization
        var organizationId = serviceAccounts.First().OrganizationId;
        if (serviceAccounts.Any(sa => sa.OrganizationId != organizationId) ||
            !_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }

        var serviceAccountsToDelete = new List<ServiceAccount>();
        var results = new List<(ServiceAccount ServiceAccount, string Error)>();

        foreach (var serviceAccount in serviceAccounts)
        {
            var authorizationResult =
                await _authorizationService.AuthorizeAsync(User, serviceAccount, ServiceAccountOperations.Delete);
            if (authorizationResult.Succeeded)
            {
                serviceAccountsToDelete.Add(serviceAccount);
                results.Add((serviceAccount, ""));
            }
            else
            {
                results.Add((serviceAccount, "access denied"));
            }
        }

        await _deleteServiceAccountsCommand.DeleteServiceAccounts(serviceAccountsToDelete);
        var responses = results.Select(r => new BulkDeleteResponseModel(r.ServiceAccount.Id, r.Error));
        return new ListResponseModel<BulkDeleteResponseModel>(responses);
    }

    [HttpGet("{id}/access-tokens")]
    public async Task<ListResponseModel<AccessTokenResponseModel>> GetAccessTokens([FromRoute] Guid id)
    {
        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(id);
        var authorizationResult =
            await _authorizationService.AuthorizeAsync(User, serviceAccount,
                ServiceAccountOperations.ReadAccessTokens);

        if (!authorizationResult.Succeeded)
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
        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(id);
        var authorizationResult =
            await _authorizationService.AuthorizeAsync(User, serviceAccount,
                ServiceAccountOperations.CreateAccessToken);

        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        var result = await _createAccessTokenCommand.CreateAsync(request.ToApiKey(id));
        return new AccessTokenCreationResponseModel(result);
    }

    [HttpPost("{id}/access-tokens/revoke")]
    public async Task RevokeAccessTokensAsync(Guid id, [FromBody] RevokeAccessTokensRequest request)
    {
        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(id);
        var authorizationResult =
            await _authorizationService.AuthorizeAsync(User, serviceAccount,
                ServiceAccountOperations.RevokeAccessTokens);

        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        await _revokeAccessTokensCommand.RevokeAsync(serviceAccount, request.Ids);
    }
}
