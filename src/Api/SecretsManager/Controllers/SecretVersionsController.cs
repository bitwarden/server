using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Auth.Identity;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.HttpExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.SecretsManager.Controllers;

[Authorize("secrets")]
public class SecretVersionsController : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly ISecretVersionRepository _secretVersionRepository;
    private readonly ISecretRepository _secretRepository;
    private readonly IUserService _userService;
    private readonly IBuildSecretVersionCommand _buildSecretVersionCommand;

    public SecretVersionsController(
        ICurrentContext currentContext,
        ISecretVersionRepository secretVersionRepository,
        ISecretRepository secretRepository,
        IUserService userService,
        IBuildSecretVersionCommand buildSecretVersionCommand)
    {
        _currentContext = currentContext;
        _secretVersionRepository = secretVersionRepository;
        _secretRepository = secretRepository;
        _userService = userService;
        _buildSecretVersionCommand = buildSecretVersionCommand;
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

        if (!CanReadEditorNames(accessClient))
        {
            var versionsWithoutEditors = await _secretVersionRepository.GetManyBySecretIdAsync(secretId);

            return new ListResponseModel<SecretVersionResponseModel>(
                versionsWithoutEditors.Select(v => new SecretVersionResponseModel(v)));
        }

        var versions = await _secretVersionRepository.GetManyDetailsBySecretIdAsync(secretId);
        var responses = versions.Select(v => new SecretVersionResponseModel(v));

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

        var restoredVersion = valueChanged
            ? await _buildSecretVersionCommand.BuildAsync(secret, accessClientId)
            : null;

        var updatedSecret = await _secretRepository.UpdateAsync(secret, null, restoredVersion);

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
