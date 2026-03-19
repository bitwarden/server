using Bit.Api.Vault.Models.Request;
using Bit.Api.Vault.Models.Response;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Vault.Commands.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Vault.Controllers;

/// <summary>
/// Manages collection-scoped API keys for AI agents and machine clients.
/// These keys provide vault access limited to a specific collection within an organization.
/// </summary>
[Route("organizations/{orgId}/collections/{collectionId}/api-keys")]
[Authorize("Application")]
public class CollectionApiKeysController : Controller
{
    private readonly ICollectionRepository _collectionRepository;
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly ICreateCollectionApiKeyCommand _createCollectionApiKeyCommand;
    private readonly ICurrentContext _currentContext;

    public CollectionApiKeysController(
        ICollectionRepository collectionRepository,
        IApiKeyRepository apiKeyRepository,
        ICreateCollectionApiKeyCommand createCollectionApiKeyCommand,
        ICurrentContext currentContext)
    {
        _collectionRepository = collectionRepository;
        _apiKeyRepository = apiKeyRepository;
        _createCollectionApiKeyCommand = createCollectionApiKeyCommand;
        _currentContext = currentContext;
    }

    /// <summary>
    /// Creates a new collection-scoped API key. Only organization admins/owners can create these.
    /// The client_secret is returned ONCE in the response and cannot be retrieved again.
    /// </summary>
    [HttpPost]
    public async Task<CollectionApiKeyCreationResponseModel> CreateAsync(
        [FromRoute] Guid orgId,
        [FromRoute] Guid collectionId,
        [FromBody] CollectionApiKeyCreateRequestModel request)
    {
        // Verify the caller is an org admin or owner
        if (!await _currentContext.OrganizationAdmin(orgId) &&
            !await _currentContext.OrganizationOwner(orgId))
        {
            throw new NotFoundException();
        }

        // Verify the collection exists and belongs to this org
        var collection = await _collectionRepository.GetByIdAsync(collectionId);
        if (collection == null || collection.OrganizationId != orgId)
        {
            throw new NotFoundException();
        }

        var apiKey = request.ToApiKey(orgId, collectionId);
        var result = await _createCollectionApiKeyCommand.CreateAsync(apiKey);

        return new CollectionApiKeyCreationResponseModel(result);
    }

    /// <summary>
    /// Lists all API keys for a collection. Does not return client secrets.
    /// </summary>
    [HttpGet]
    public async Task<IEnumerable<CollectionApiKeyResponseModel>> ListAsync(
        [FromRoute] Guid orgId,
        [FromRoute] Guid collectionId)
    {
        if (!await _currentContext.OrganizationAdmin(orgId) &&
            !await _currentContext.OrganizationOwner(orgId))
        {
            throw new NotFoundException();
        }

        var collection = await _collectionRepository.GetByIdAsync(collectionId);
        if (collection == null || collection.OrganizationId != orgId)
        {
            throw new NotFoundException();
        }

        var apiKeys = await _apiKeyRepository.GetManyByCollectionIdAsync(collectionId);
        return apiKeys.Select(k => new CollectionApiKeyResponseModel(k));
    }

    /// <summary>
    /// Revokes (deletes) a collection API key.
    /// </summary>
    [HttpDelete("{apiKeyId}")]
    public async Task RevokeAsync(
        [FromRoute] Guid orgId,
        [FromRoute] Guid collectionId,
        [FromRoute] Guid apiKeyId)
    {
        if (!await _currentContext.OrganizationAdmin(orgId) &&
            !await _currentContext.OrganizationOwner(orgId))
        {
            throw new NotFoundException();
        }

        var apiKey = await _apiKeyRepository.GetByIdAsync(apiKeyId);
        if (apiKey == null || apiKey.CollectionId != collectionId || apiKey.OrganizationId != orgId)
        {
            throw new NotFoundException();
        }

        await _apiKeyRepository.DeleteAsync(apiKey);
    }
}
