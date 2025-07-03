﻿using System.Net;
using Bit.Api.Models.Public.Request;
using Bit.Api.Models.Public.Response;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.OrganizationFeatures.OrganizationCollections.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Public.Controllers;

[Route("public/collections")]
[Authorize("Organization")]
public class CollectionsController : Controller
{
    private readonly ICollectionRepository _collectionRepository;
    private readonly IUpdateCollectionCommand _updateCollectionCommand;
    private readonly ICurrentContext _currentContext;
    private readonly IApplicationCacheService _applicationCacheService;

    public CollectionsController(
        ICollectionRepository collectionRepository,
        IUpdateCollectionCommand updateCollectionCommand,
        ICurrentContext currentContext,
        IApplicationCacheService applicationCacheService)
    {
        _collectionRepository = collectionRepository;
        _updateCollectionCommand = updateCollectionCommand;
        _currentContext = currentContext;
        _applicationCacheService = applicationCacheService;
    }

    /// <summary>
    /// Retrieve a collection.
    /// </summary>
    /// <remarks>
    /// Retrieves the details of an existing collection. You need only supply the unique collection identifier
    /// that was returned upon collection creation.
    /// </remarks>
    /// <param name="id">The identifier of the collection to be retrieved.</param>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CollectionResponseModel), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> Get(Guid id)
    {
        (var collection, var access) = await _collectionRepository.GetByIdWithAccessAsync(id);
        if (collection == null || collection.OrganizationId != _currentContext.OrganizationId)
        {
            return new NotFoundResult();
        }
        var response = new CollectionResponseModel(collection, access.Groups);
        return new JsonResult(response);
    }

    /// <summary>
    /// List all collections.
    /// </summary>
    /// <remarks>
    /// Returns a list of your organization's collections.
    /// Collection objects listed in this call do not include information about their associated groups.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(ListResponseModel<CollectionResponseModel>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> List()
    {
        var collections = await _collectionRepository.GetManyByOrganizationIdAsync(
            _currentContext.OrganizationId.Value);
        // TODO: Get all CollectionGroup associations for the organization and marry them up here for the response.
        var collectionResponses = collections.Select(c => new CollectionResponseModel(c, null));
        var response = new ListResponseModel<CollectionResponseModel>(collectionResponses);
        return new JsonResult(response);
    }

    /// <summary>
    /// Update a collection.
    /// </summary>
    /// <remarks>
    /// Updates the specified collection object. If a property is not provided,
    /// the value of the existing property will be reset.
    /// </remarks>
    /// <param name="id">The identifier of the collection to be updated.</param>
    /// <param name="model">The request model.</param>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(CollectionResponseModel), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponseModel), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> Put(Guid id, [FromBody] CollectionUpdateRequestModel model)
    {
        var existingCollection = await _collectionRepository.GetByIdAsync(id);
        if (existingCollection == null || existingCollection.OrganizationId != _currentContext.OrganizationId)
        {
            return new NotFoundResult();
        }
        var updatedCollection = model.ToCollection(existingCollection);
        var associations = model.Groups?.Select(c => c.ToCollectionAccessSelection()).ToList();
        await _updateCollectionCommand.UpdateAsync(updatedCollection, associations, null);
        var response = new CollectionResponseModel(updatedCollection, associations);
        return new JsonResult(response);
    }

    /// <summary>
    /// Delete a collection.
    /// </summary>
    /// <remarks>
    /// Permanently deletes a collection. This cannot be undone.
    /// </remarks>
    /// <param name="id">The identifier of the collection to be deleted.</param>
    [HttpDelete("{id}")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var collection = await _collectionRepository.GetByIdAsync(id);
        if (collection == null || collection.OrganizationId != _currentContext.OrganizationId)
        {
            return new NotFoundResult();
        }

        if (collection.Type == CollectionType.DefaultUserCollection)
        {
            return new BadRequestObjectResult(new ErrorResponseModel("You cannot delete a collection with the type as DefaultUserCollection."));
        }

        await _collectionRepository.DeleteAsync(collection);
        return new OkResult();
    }
}
