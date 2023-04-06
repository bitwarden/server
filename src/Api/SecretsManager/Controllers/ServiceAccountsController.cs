﻿using Bit.Api.Models.Response;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.AccessTokens.Interfaces;
using Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;
using Bit.Core.SecretsManager.Queries.Access.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.SecretsManager.Controllers;

[SecretsManager]
[Authorize("secrets")]
[Route("service-accounts")]
public class ServiceAccountsController : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly IUserService _userService;
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly ICreateAccessTokenCommand _createAccessTokenCommand;
    private readonly ICreateServiceAccountCommand _createServiceAccountCommand;
    private readonly IUpdateServiceAccountCommand _updateServiceAccountCommand;
    private readonly IDeleteServiceAccountsCommand _deleteServiceAccountsCommand;
    private readonly IRevokeAccessTokensCommand _revokeAccessTokensCommand;
    private readonly IAccessQuery _accessQuery;

    public ServiceAccountsController(
        ICurrentContext currentContext,
        IUserService userService,
        IServiceAccountRepository serviceAccountRepository,
        IApiKeyRepository apiKeyRepository,
        ICreateAccessTokenCommand createAccessTokenCommand,
        ICreateServiceAccountCommand createServiceAccountCommand,
        IUpdateServiceAccountCommand updateServiceAccountCommand,
        IDeleteServiceAccountsCommand deleteServiceAccountsCommand,
        IRevokeAccessTokensCommand revokeAccessTokensCommand,
        IAccessQuery accessQuery)
    {
        _currentContext = currentContext;
        _userService = userService;
        _serviceAccountRepository = serviceAccountRepository;
        _apiKeyRepository = apiKeyRepository;
        _createServiceAccountCommand = createServiceAccountCommand;
        _updateServiceAccountCommand = updateServiceAccountCommand;
        _deleteServiceAccountsCommand = deleteServiceAccountsCommand;
        _revokeAccessTokensCommand = revokeAccessTokensCommand;
        _createAccessTokenCommand = createAccessTokenCommand;
        _accessQuery = accessQuery;
    }

    [HttpGet("/organizations/{organizationId}/service-accounts")]
    public async Task<ListResponseModel<ServiceAccountResponseModel>> ListByOrganizationAsync(
        [FromRoute] Guid organizationId)
    {
        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var serviceAccounts =
            await _serviceAccountRepository.GetManyByOrganizationIdAsync(organizationId, userId, accessClient);

        var responses = serviceAccounts.Select(serviceAccount => new ServiceAccountResponseModel(serviceAccount));
        return new ListResponseModel<ServiceAccountResponseModel>(responses);
    }

    [HttpGet("{id}")]
    public async Task<ServiceAccountResponseModel> GetByServiceAccountIdAsync(
     [FromRoute] Guid id)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(id);

        if (serviceAccount == null)
        {
            throw new NotFoundException();
        }

        if (!_currentContext.AccessSecretsManager(serviceAccount.OrganizationId))
        {
            throw new NotFoundException();
        }

        var orgAdmin = await _currentContext.OrganizationAdmin(serviceAccount.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var access = await _serviceAccountRepository.AccessToServiceAccountAsync(id, userId, accessClient);
        if (!access.Write)
        {
            throw new NotFoundException();
        }

        return new ServiceAccountResponseModel(serviceAccount);
    }

    [HttpPost("/organizations/{organizationId}/service-accounts")]
    public async Task<ServiceAccountResponseModel> CreateAsync([FromRoute] Guid organizationId,
        [FromBody] ServiceAccountCreateRequestModel createRequest)
    {
        var userId = _userService.GetProperUserId(User).Value;
        if (!await _accessQuery.HasAccess(createRequest.ToAccessCheck(organizationId, userId)))
        {
            throw new NotFoundException();
        }

        var result = await _createServiceAccountCommand.CreateAsync(createRequest.ToServiceAccount(organizationId), userId);
        return new ServiceAccountResponseModel(result);
    }

    [HttpPut("{id}")]
    public async Task<ServiceAccountResponseModel> UpdateAsync([FromRoute] Guid id,
        [FromBody] ServiceAccountUpdateRequestModel updateRequest)
    {
        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(id);
        if (serviceAccount == null)
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        if (!await _accessQuery.HasAccess(updateRequest.ToAccessCheck(id, serviceAccount.OrganizationId, userId)))
        {
            throw new NotFoundException();
        }

        var result = await _updateServiceAccountCommand.UpdateAsync(updateRequest.ToServiceAccount(id));
        return new ServiceAccountResponseModel(result);
    }

    [HttpPost("delete")]
    public async Task<ListResponseModel<BulkDeleteResponseModel>> BulkDeleteAsync([FromBody] List<Guid> ids)
    {
        var userId = _userService.GetProperUserId(User).Value;

        var results = await _deleteServiceAccountsCommand.DeleteServiceAccounts(ids, userId);
        var responses = results.Select(r => new BulkDeleteResponseModel(r.Item1.Id, r.Item2));
        return new ListResponseModel<BulkDeleteResponseModel>(responses);
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

        if (!_currentContext.AccessSecretsManager(serviceAccount.OrganizationId))
        {
            throw new NotFoundException();
        }

        var orgAdmin = await _currentContext.OrganizationAdmin(serviceAccount.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var access = await _serviceAccountRepository.AccessToServiceAccountAsync(id, userId, accessClient);
        if (!access.Read)
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
        if (serviceAccount == null)
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        if (!await _accessQuery.HasAccess(request.ToAccessCheck(id, serviceAccount.OrganizationId, userId)))
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
        if (serviceAccount == null)
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        if (!await _accessQuery.HasAccess(request.ToAccessCheck(id, serviceAccount.OrganizationId, userId)))
        {
            throw new NotFoundException();
        }

        await _revokeAccessTokensCommand.RevokeAsync(serviceAccount, request.Ids);
    }
}
