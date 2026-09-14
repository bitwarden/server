using Bit.Api.Models.Response;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core;
using Bit.Core.Auth.Identity;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bitwarden.Server.Sdk.Features;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.SecretsManager.Controllers;

/// <summary>
/// Secret version history. The whole controller sits behind the SecretsVersioning feature flag, so
/// none of the version reads, restores, or their event logs happen while the flag is off.
/// </summary>
[Authorize("secrets")]
[RequireFeature(FeatureFlagKeys.SecretsVersioning)]
public class SecretVersionsController : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly ISecretVersionRepository _secretVersionRepository;
    private readonly ISecretRepository _secretRepository;
    private readonly IUserService _userService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IEventService _eventService;

    public SecretVersionsController(
        ICurrentContext currentContext,
        ISecretVersionRepository secretVersionRepository,
        ISecretRepository secretRepository,
        IUserService userService,
        IOrganizationUserRepository organizationUserRepository,
        IEventService eventService)
    {
        _currentContext = currentContext;
        _secretVersionRepository = secretVersionRepository;
        _secretRepository = secretRepository;
        _userService = userService;
        _organizationUserRepository = organizationUserRepository;
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

        var userId = _userService.GetProperUserId(User);
        if (!userId.HasValue)
        {
            throw new NotFoundException();
        }

        var orgAdmin = await _currentContext.OrganizationAdmin(secret.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.IdentityClientType, orgAdmin);

        var access = await _secretRepository.AccessToSecretAsync(secretId, userId.Value, accessClient);
        if (!access.Read)
        {
            throw new NotFoundException();
        }

        var versions = (await _secretVersionRepository.GetManyBySecretIdAsync(secretId)).ToList();
        if (versions.Count > 0)
        {
            // Each version carries a value the secret once held, so reading history is a secret
            // retrieval for audit purposes, the same as reading the current value.
            await LogSecretEventAsync(secret, EventType.Secret_Retrieved);
        }

        var responses = versions.Select(v => new SecretVersionResponseModel(v)).ToList();

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

        var userId = _userService.GetProperUserId(User);
        if (!userId.HasValue)
        {
            throw new NotFoundException();
        }

        var orgAdmin = await _currentContext.OrganizationAdmin(secret.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.IdentityClientType, orgAdmin);

        var access = await _secretRepository.AccessToSecretAsync(secretVersion.SecretId, userId.Value, accessClient);
        if (!access.Read)
        {
            throw new NotFoundException();
        }

        await LogSecretEventAsync(secret, EventType.Secret_Retrieved);

        return new SecretVersionResponseModel(secretVersion);
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

        var userId = _userService.GetProperUserId(User);
        if (!userId.HasValue)
        {
            throw new NotFoundException();
        }

        var isAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.IdentityClientType, isAdmin);

        var accessResults = await _secretRepository.AccessToSecretsAsync(secretIds, userId.Value, accessClient);
        if (accessResults.Count != secretIds.Count || accessResults.Values.Any(access => !access.Read))
        {
            throw new NotFoundException();
        }

        await LogSecretsEventAsync(secrets, EventType.Secret_Retrieved);

        var responses = versions.Select(v => new SecretVersionResponseModel(v));
        return new ListResponseModel<SecretVersionResponseModel>(responses);
    }

    [HttpPut("secrets/{secretId}/versions/restore")]
    public async Task<SecretResponseModel> RestoreVersionAsync([FromRoute] Guid secretId, [FromBody] RestoreSecretVersionRequestModel request)
    {
        if (!(_currentContext.IdentityClientType == IdentityClientType.User || _currentContext.IdentityClientType == IdentityClientType.ServiceAccount))
        {
            throw new NotFoundException();
        }

        var secret = await _secretRepository.GetByIdAsync(secretId);
        if (secret == null || !_currentContext.AccessSecretsManager(secret.OrganizationId))
        {
            throw new NotFoundException();
        }

        var version = await _secretVersionRepository.GetByIdAsync(request.VersionId);
        if (version == null || version.SecretId != secretId)
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User);
        if (!userId.HasValue)
        {
            throw new NotFoundException();
        }

        var orgAdmin = await _currentContext.OrganizationAdmin(secret.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.IdentityClientType, orgAdmin);

        var access = await _secretRepository.AccessToSecretAsync(secretId, userId.Value, accessClient);
        if (!access.Write)
        {
            throw new NotFoundException();
        }

        // Captured before the restore overwrites them: the displaced value is snapshotted with the
        // date it was set, not the date it was replaced, so history stays in the order it happened.
        var currentValue = secret.Value;
        var currentValueRevisionDate = secret.RevisionDate;

        if (currentValue != version.Value)
        {
            Guid? editorServiceAccountId = null;
            Guid? editorOrganizationUserId = null;

            if (_currentContext.IdentityClientType == IdentityClientType.ServiceAccount)
            {
                editorServiceAccountId = userId.Value;
            }
            else
            {
                var orgUser = await _organizationUserRepository.GetByOrganizationAsync(secret.OrganizationId, userId.Value);
                if (orgUser == null)
                {
                    throw new NotFoundException();
                }

                editorOrganizationUserId = orgUser.Id;
            }

            var currentVersionSnapshot = new SecretVersion
            {
                SecretId = secretId,
                Value = currentValue!,
                VersionDate = currentValueRevisionDate,
                EditorServiceAccountId = editorServiceAccountId,
                EditorOrganizationUserId = editorOrganizationUserId
            };

            await _secretVersionRepository.CreateAsync(currentVersionSnapshot);
        }

        secret.Value = version.Value;
        secret.RevisionDate = DateTime.UtcNow;

        var updatedSecret = await _secretRepository.UpdateAsync(secret);

        // A restore changes the secret's current value, so it is audited as an edit.
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

        var userId = _userService.GetProperUserId(User);
        if (!userId.HasValue)
        {
            throw new NotFoundException();
        }

        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.IdentityClientType, orgAdmin);

        var accessResults = await _secretRepository.AccessToSecretsAsync(secretIds, userId.Value, accessClient);
        if (accessResults.Count != secretIds.Count || accessResults.Values.Any(access => !access.Write))
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
}
