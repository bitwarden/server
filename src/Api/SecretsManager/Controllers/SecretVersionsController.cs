using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core;
using Bit.Core.Auth.Identity;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.HttpExtensions;
using Bitwarden.Server.Sdk.Features;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.SecretsManager.Controllers;

[Authorize("secrets")]
[RequireFeature(FeatureFlagKeys.SecretsVersioning)]
public class SecretVersionsController : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly ISecretVersionRepository _secretVersionRepository;
    private readonly ISecretRepository _secretRepository;
    private readonly IUserService _userService;
    private readonly IUpdateSecretCommand _updateSecretCommand;
    private readonly IEventService _eventService;

    public SecretVersionsController(
        ICurrentContext currentContext,
        ISecretVersionRepository secretVersionRepository,
        ISecretRepository secretRepository,
        IUserService userService,
        IUpdateSecretCommand updateSecretCommand,
        IEventService eventService)
    {
        _currentContext = currentContext;
        _secretVersionRepository = secretVersionRepository;
        _secretRepository = secretRepository;
        _userService = userService;
        _updateSecretCommand = updateSecretCommand;
        _eventService = eventService;
    }

    [HttpGet("secrets/{secretId}/versions")]
    public async Task<ListResponseModel<SecretVersionResponseModel>> GetVersionsBySecretIdAsync([FromRoute] Guid secretId)
    {
        var secret = await _secretRepository.GetByIdAsync(secretId);
        if (secret == null || !_currentContext.AccessSecretsManager(secret.OrganizationId))
        {
            throw new NotFoundException();
        }

        var (accessClient, accessClientId) = await GetAccessContextAsync(secret.OrganizationId);

        var access = await _secretRepository.AccessToSecretAsync(secretId, accessClientId, accessClient);
        if (!access.Read)
        {
            throw new NotFoundException();
        }

        var responses = CanReadEditorNames(accessClient)
            ? (await _secretVersionRepository.GetManyDetailsBySecretIdAsync(secretId))
                .Select(v => new SecretVersionResponseModel(v)).ToList()
            : (await _secretVersionRepository.GetManyBySecretIdAsync(secretId))
                .Select(v => new SecretVersionResponseModel(v)).ToList();

        if (responses.Count > 0)
        {
            // Each version carries a value the secret once held, so reading history is a secret
            // retrieval for audit purposes, the same as reading the current value.
            await LogSecretEventAsync(secret, EventType.Secret_Retrieved);
        }

        return new ListResponseModel<SecretVersionResponseModel>(responses);
    }

    [HttpGet("secret-versions/{id}")]
    public async Task<SecretVersionResponseModel> GetByIdAsync([FromRoute] Guid id)
    {
        var secretVersion = await _secretVersionRepository.GetByIdAsync(id);
        if (secretVersion == null)
        {
            throw new NotFoundException();
        }

        var secret = await _secretRepository.GetByIdAsync(secretVersion.SecretId);
        if (secret == null || !_currentContext.AccessSecretsManager(secret.OrganizationId))
        {
            throw new NotFoundException();
        }

        var (accessClient, accessClientId) = await GetAccessContextAsync(secret.OrganizationId);

        var access = await _secretRepository.AccessToSecretAsync(secretVersion.SecretId, accessClientId, accessClient);
        if (!access.Read)
        {
            throw new NotFoundException();
        }

        await LogSecretEventAsync(secret, EventType.Secret_Retrieved);

        if (!CanReadEditorNames(accessClient))
        {
            return new SecretVersionResponseModel(secretVersion);
        }

        var secretVersionDetails = await _secretVersionRepository.GetDetailsByIdAsync(id);
        if (secretVersionDetails == null)
        {
            throw new NotFoundException();
        }

        return new SecretVersionResponseModel(secretVersionDetails);
    }

    [HttpPost("secret-versions/get-by-ids")]
    public async Task<ListResponseModel<SecretVersionResponseModel>> GetManyByIdsAsync([FromBody] List<Guid> ids)
    {
        if (!ids.Any())
        {
            throw new BadRequestException("No version IDs provided.");
        }

        // Get all versions
        var versions = (await _secretVersionRepository.GetManyByIdsAsync(ids)).ToList();
        if (!versions.Any())
        {
            throw new NotFoundException();
        }

        // Get all associated secrets and check permissions
        var secretIds = versions.Select(v => v.SecretId).Distinct().ToList();
        var secrets = (await _secretRepository.GetManyByIds(secretIds)).ToList();

        if (!secrets.Any())
        {
            throw new NotFoundException();
        }

        // Ensure all secrets belong to the same organization
        var organizationId = secrets.First().OrganizationId;
        if (secrets.Any(s => s.OrganizationId != organizationId) ||
            !_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }

        var (accessClient, accessClientId) = await GetAccessContextAsync(organizationId);

        var accessResults = await _secretRepository.AccessToSecretsAsync(secretIds, accessClientId, accessClient);
        if (secretIds.Any(id => !accessResults.TryGetValue(id, out var access) || !access.Read))
        {
            throw new NotFoundException();
        }

        await LogSecretsEventAsync(secrets, EventType.Secret_Retrieved);

        if (!CanReadEditorNames(accessClient))
        {
            return new ListResponseModel<SecretVersionResponseModel>(
                versions.Select(v => new SecretVersionResponseModel(v)));
        }

        var details = await _secretVersionRepository.GetManyDetailsByIdsAsync(ids);

        return new ListResponseModel<SecretVersionResponseModel>(
            details.Select(v => new SecretVersionResponseModel(v)));
    }

    [HttpPut("secrets/{secretId}/versions/restore")]
    public async Task<SecretResponseModel> RestoreVersionAsync([FromRoute] Guid secretId, [FromBody] RestoreSecretVersionRequestModel request)
    {
        if (_currentContext.IdentityClientType != IdentityClientType.User &&
            _currentContext.IdentityClientType != IdentityClientType.ServiceAccount)
        {
            throw new NotFoundException();
        }

        var secret = await _secretRepository.GetByIdAsync(secretId);
        if (secret == null || !_currentContext.AccessSecretsManager(secret.OrganizationId))
        {
            throw new NotFoundException();
        }

        var (accessClient, accessClientId) = await GetAccessContextAsync(secret.OrganizationId);

        var access = await _secretRepository.AccessToSecretAsync(secretId, accessClientId, accessClient);
        if (!access.Write)
        {
            throw new NotFoundException();
        }

        var version = await _secretVersionRepository.GetByIdAsync(request.VersionId);
        if (version == null || version.SecretId != secretId)
        {
            throw new NotFoundException();
        }

        var valueChanged = secret.Value != version.Value;

        secret.Value = version.Value;
        secret.RevisionDate = DateTime.UtcNow;

        var updatedSecret = await _updateSecretCommand.UpdateAsync(secret, null, valueChanged);

        // A restore changes the current value of the secret, so it is audited as an edit.
        await LogSecretEventAsync(updatedSecret, EventType.Secret_Edited);

        return new SecretResponseModel(updatedSecret, true, true);
    }

    [HttpPost("secret-versions/delete")]
    public async Task<IActionResult> BulkDeleteAsync([FromBody] List<Guid> ids)
    {
        if (!ids.Any())
        {
            throw new BadRequestException("No version IDs provided.");
        }

        var secretVersions = (await _secretVersionRepository.GetManyByIdsAsync(ids)).ToList();
        if (secretVersions.Count != ids.Count)
        {
            throw new NotFoundException();
        }

        // Ensure all versions belong to secrets in the same organization
        var secretIds = secretVersions.Select(v => v.SecretId).Distinct().ToList();
        var secrets = await _secretRepository.GetManyByIds(secretIds);
        var secretsList = secrets.ToList();

        if (!secretsList.Any())
        {
            throw new NotFoundException();
        }

        var organizationId = secretsList.First().OrganizationId;
        if (secretsList.Any(s => s.OrganizationId != organizationId) ||
            !_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }

        var (accessClient, accessClientId) = await GetAccessContextAsync(organizationId);

        var accessResults = await _secretRepository.AccessToSecretsAsync(secretIds, accessClientId, accessClient);
        if (secretIds.Any(id => !accessResults.TryGetValue(id, out var access) || !access.Write))
        {
            throw new NotFoundException();
        }

        await _secretVersionRepository.DeleteManyByIdAsync(ids);

        return Ok();
    }

    private async Task LogSecretsEventAsync(IEnumerable<Secret> secrets, EventType eventType)
    {
        var userId = _userService.GetProperUserId(User)!.Value;

        switch (_currentContext.IdentityClientType)
        {
            case IdentityClientType.ServiceAccount:
                await _eventService.LogServiceAccountSecretsEventAsync(userId, secrets, eventType);
                break;
            case IdentityClientType.User:
                await _eventService.LogUserSecretsEventAsync(userId, secrets, eventType);
                break;
        }
    }

    private Task LogSecretEventAsync(Secret secret, EventType eventType) =>
        LogSecretsEventAsync(new[] { secret }, eventType);

    private static bool CanReadEditorNames(AccessClientType accessClient) =>
        accessClient is AccessClientType.User or AccessClientType.NoAccessCheck;

    private async Task<(AccessClientType AccessClient, Guid AccessClientId)> GetAccessContextAsync(Guid organizationId)
    {
        var accessClientId = _userService.GetProperUserId(User);
        if (!accessClientId.HasValue)
        {
            throw new NotFoundException();
        }

        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.IdentityClientType, orgAdmin);

        return (accessClient, accessClientId.Value);
    }
}
