using Bit.Api.Models.Response;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Auth.Identity;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
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
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public SecretVersionsController(
        ICurrentContext currentContext,
        ISecretVersionRepository secretVersionRepository,
        ISecretRepository secretRepository,
        IUserService userService,
        IOrganizationUserRepository organizationUserRepository)
    {
        _currentContext = currentContext;
        _secretVersionRepository = secretVersionRepository;
        _secretRepository = secretRepository;
        _userService = userService;
        _organizationUserRepository = organizationUserRepository;
    }

    [HttpGet("secrets/{secretId}/versions")]
    public async Task<ListResponseModel<SecretVersionResponseModel>> GetVersionsBySecretIdAsync([FromRoute] Guid secretId)
    {
        var secret = await _secretRepository.GetByIdAsync(secretId);
        if (secret == null || !_currentContext.AccessSecretsManager(secret.OrganizationId))
        {
            throw new NotFoundException();
        }

        // For service accounts and organization API, skip user-level access checks
        if (_currentContext.IdentityClientType == IdentityClientType.ServiceAccount ||
            _currentContext.IdentityClientType == IdentityClientType.Organization)
        {
            // Already verified Secrets Manager access above
            var versionList = await _secretVersionRepository.GetManyBySecretIdAsync(secretId);
            var responseList = versionList.Select(v => new SecretVersionResponseModel(v));
            return new ListResponseModel<SecretVersionResponseModel>(responseList);
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

        // For service accounts and organization API, skip user-level access checks
        if (_currentContext.IdentityClientType == IdentityClientType.ServiceAccount ||
            _currentContext.IdentityClientType == IdentityClientType.Organization)
        {
            // Already verified Secrets Manager access above
            return new SecretVersionResponseModel(secretVersion);
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

        // For service accounts and organization API, skip user-level access checks
        if (_currentContext.IdentityClientType == IdentityClientType.ServiceAccount ||
            _currentContext.IdentityClientType == IdentityClientType.Organization)
        {
            // Already verified Secrets Manager access and organization ownership above
            var serviceAccountResponses = versions.Select(v => new SecretVersionResponseModel(v));
            return new ListResponseModel<SecretVersionResponseModel>(serviceAccountResponses);
        }

        var userId = _userService.GetProperUserId(User);
        if (!userId.HasValue)
        {
            throw new NotFoundException();
        }

        var isAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.IdentityClientType, isAdmin);

        // Verify read access to all associated secrets
        var accessResults = await _secretRepository.AccessToSecretsAsync(secretIds, userId.Value, accessClient);
        if (accessResults.Values.Any(access => !access.Read))
        {
            throw new NotFoundException();
        }

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

        // Get the version first to validate it belongs to this secret
        var version = await _secretVersionRepository.GetByIdAsync(request.VersionId);
        if (version == null || version.SecretId != secretId)
        {
            throw new NotFoundException();
        }

        // Store the current value before restoration
        var currentValue = secret.Value;

        // For service accounts and organization API, skip user-level access checks
        if (_currentContext.IdentityClientType == IdentityClientType.ServiceAccount)
        {
            // Save current value as a version before restoring
            if (currentValue != version.Value)
            {
                var editorUserId = _userService.GetProperUserId(User);
                if (editorUserId.HasValue)
                {
                    var currentVersionSnapshot = new Core.SecretsManager.Entities.SecretVersion
                    {
                        SecretId = secretId,
                        Value = currentValue!,
                        VersionDate = DateTime.UtcNow,
                        EditorServiceAccountId = editorUserId.Value
                    };

                    await _secretVersionRepository.CreateAsync(currentVersionSnapshot);
                }
            }

            // Already verified Secrets Manager access above
            secret.Value = version.Value;
            secret.RevisionDate = DateTime.UtcNow;
            var updatedSec = await _secretRepository.UpdateAsync(secret);
            return new SecretResponseModel(updatedSec, true, true);
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

        // Save current value as a version before restoring
        if (currentValue != version.Value)
        {
            var orgUser = await _organizationUserRepository.GetByOrganizationAsync(secret.OrganizationId, userId.Value);
            if (orgUser == null)
            {
                throw new NotFoundException();
            }

            var currentVersionSnapshot = new Core.SecretsManager.Entities.SecretVersion
            {
                SecretId = secretId,
                Value = currentValue!,
                VersionDate = DateTime.UtcNow,
                EditorOrganizationUserId = orgUser.Id
            };

            await _secretVersionRepository.CreateAsync(currentVersionSnapshot);
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

        // For service accounts and organization API, skip user-level access checks
        if (_currentContext.IdentityClientType == IdentityClientType.ServiceAccount ||
            _currentContext.IdentityClientType == IdentityClientType.Organization)
        {
            // Already verified Secrets Manager access and organization ownership above
            await _secretVersionRepository.DeleteManyByIdAsync(ids);
            return Ok();
        }

        var userId = _userService.GetProperUserId(User);
        if (!userId.HasValue)
        {
            throw new NotFoundException();
        }

        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.IdentityClientType, orgAdmin);

        // Verify write access to all associated secrets
        var accessResults = await _secretRepository.AccessToSecretsAsync(secretIds, userId.Value, accessClient);
        if (accessResults.Values.Any(access => !access.Write))
        {
            throw new NotFoundException();
        }

        await _secretVersionRepository.DeleteManyByIdAsync(ids);

        return Ok();
    }
}
