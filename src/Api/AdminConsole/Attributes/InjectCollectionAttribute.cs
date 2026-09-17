using Bit.Api.AdminConsole.Authorization;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace Bit.Api.AdminConsole.Attributes;

/// <summary>
/// Binds a <see cref="Bit.Core.Entities.Collection"/> parameter by loading it from the database
/// and validating that it belongs to the organization identified by the <c>orgId</c> or
/// <c>organizationId</c> route parameter.
/// </summary>
/// <remarks>
/// The collection is resolved from the route parameter named by
/// <see cref="CollectionIdRouteParam"/> (default <c>"id"</c>). If the collection is not found or
/// does not belong to the organization, a <see cref="Bit.Core.Exceptions.NotFoundException"/> is thrown.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// [HttpGet("{id}")]
/// public async Task<CollectionResponseModel> Get(Guid orgId,
///     [InjectCollection] Collection collection)
///
/// [HttpDelete("{collectionId}")]
/// public async Task Delete(Guid orgId,
///     [InjectCollection("collectionId")] Collection collection)
/// ]]></code>
/// </example>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class InjectCollectionAttribute(string collectionIdRouteParam = "id")
    : ModelBinderAttribute(typeof(CollectionModelBinder))
{
    /// <summary>
    /// Name of the route parameter containing the collection ID. Defaults to <c>"id"</c>.
    /// </summary>
    public string CollectionIdRouteParam { get; } = collectionIdRouteParam;
}

/// <summary>
/// Custom model binder that loads a <see cref="Bit.Core.Entities.Collection"/> from the database,
/// validates that it belongs to the organization identified by the route, and binds it to the parameter.
/// </summary>
/// <remarks>
/// This binder is used via the <see cref="InjectCollectionAttribute"/>.
/// </remarks>
public class CollectionModelBinder : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var defaultMetadata = bindingContext.ModelMetadata as DefaultModelMetadata;
        var attr = defaultMetadata?.Attributes.ParameterAttributes
            ?.OfType<InjectCollectionAttribute>()
            .FirstOrDefault()
            ?? new InjectCollectionAttribute();

        Guid orgId;
        try
        {
            orgId = bindingContext.HttpContext.GetOrganizationId();
        }
        catch (InvalidOperationException)
        {
            throw new BadRequestException("Route parameter 'orgId' or 'organizationId' is missing or invalid.");
        }

        var collectionId = bindingContext.HttpContext.TryGetRouteParameterAsGuid(attr.CollectionIdRouteParam);
        if (collectionId is null)
        {
            throw new BadRequestException(
                $"Route parameter '{attr.CollectionIdRouteParam}' is missing or invalid.");
        }

        var repo = bindingContext.HttpContext.RequestServices
            .GetRequiredService<ICollectionRepository>();

        var collection = await repo.GetByIdAsync(collectionId.Value);
        if (collection is null || collection.OrganizationId != orgId)
        {
            throw new NotFoundException();
        }

        bindingContext.Result = ModelBindingResult.Success(collection);
    }
}
