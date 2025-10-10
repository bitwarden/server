// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.Models.Response;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
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

    public SecretVersionsController(
        ICurrentContext currentContext,
        ISecretVersionRepository secretVersionRepository,
        ISecretRepository secretRepository,
        IUserService userService)
    {
        _currentContext = currentContext;
        _secretVersionRepository = secretVersionRepository;
        _secretRepository = secretRepository;
        _userService = userService;
    }

    [HttpGet("secrets/{secretId}/versions")]
    public async Task<ListResponseModel<SecretVersionResponseModel>> GetVersionsBySecretIdAsync([FromRoute] Guid secretId)
    {
        var secret = await _secretRepository.GetByIdAsync(secretId);
        if (secret == null || !_currentContext.AccessSecretsManager(secret.OrganizationId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(secret.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.IdentityClientType, orgAdmin);

        var access = await _secretRepository.AccessToSecretAsync(secretId, userId, accessClient);
        if (!access.Read)
        {
            throw new NotFoundException();
        }

        var versions = await _secretVersionRepository.GetManyBySecretIdAsync(secretId);
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

        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(secret.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.IdentityClientType, orgAdmin);

        var access = await _secretRepository.AccessToSecretAsync(secretVersion.SecretId, userId, accessClient);
        if (!access.Read)
        {
            throw new NotFoundException();
        }

        return new SecretVersionResponseModel(secretVersion);
    }

    [HttpPut("secrets/{secretId}/versions/restore")]
    public async Task<SecretResponseModel> RestoreVersionAsync([FromRoute] Guid secretId, [FromBody] RestoreSecretVersionRequestModel request)
    {
        var secret = await _secretRepository.GetByIdAsync(secretId);
        if (secret == null || !_currentContext.AccessSecretsManager(secret.OrganizationId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(secret.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.IdentityClientType, orgAdmin);

        var access = await _secretRepository.AccessToSecretAsync(secretId, userId, accessClient);
        if (!access.Write)
        {
            throw new NotFoundException();
        }

        var version = await _secretVersionRepository.GetByIdAsync(request.VersionId);
        if (version == null || version.SecretId != secretId)
        {
            throw new NotFoundException();
        }

        // Update the secret with the version's value
        secret.Value = version.Value;
        secret.RevisionDate = DateTime.UtcNow;

        var updatedSecret = await _secretRepository.UpdateAsync(secret);

        return new SecretResponseModel(updatedSecret, true, true);
    }

    [HttpPost("secret-versions/delete")]
    public async Task<IActionResult> BulkDeleteAsync([FromBody] List<Guid> ids)
    {
        if (!ids.Any())
        {
            throw new BadRequestException("No version IDs provided.");
        }

        // Get all versions and check permissions on their associated secrets
        var versions = new List<Core.SecretsManager.Entities.SecretVersion>();
        foreach (var id in ids)
        {
            var version = await _secretVersionRepository.GetByIdAsync(id);
            if (version == null)
            {
                throw new NotFoundException();
            }
            versions.Add(version);
        }

        // Ensure all versions belong to secrets in the same organization
        var secretIds = versions.Select(v => v.SecretId).Distinct().ToList();
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

        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.IdentityClientType, orgAdmin);

        // Verify write access to all associated secrets
        foreach (var secretId in secretIds)
        {
            var access = await _secretRepository.AccessToSecretAsync(secretId, userId, accessClient);
            if (!access.Write)
            {
                throw new NotFoundException();
            }
        }

        await _secretVersionRepository.DeleteManyByIdAsync(ids);

        return Ok();
    }
}
