using System.Reflection;
using Bit.Api.AdminConsole.Attributes;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Attributes;

public class CollectionModelBinderTests
{
    private readonly ICollectionRepository _collectionRepository;
    private readonly Collection _collection;
    private readonly Guid _orgId;
    private readonly Guid _collectionId;

    public CollectionModelBinderTests()
    {
        _collectionRepository = Substitute.For<ICollectionRepository>();
        _orgId = Guid.NewGuid();
        _collectionId = Guid.NewGuid();
        _collection = new Collection { Id = _collectionId, OrganizationId = _orgId };
    }

    [Fact]
    public async Task BindModelAsync_CollectionExistsAndBelongsToOrg_BindsSuccessfully()
    {
        var binder = new CollectionModelBinder();
        _collectionRepository.GetByIdAsync(_collectionId).Returns(_collection);

        var context = CreateBindingContext();

        await binder.BindModelAsync(context);

        Assert.True(context.Result.IsModelSet);
        Assert.Equal(_collection, context.Result.Model);
    }

    [Fact]
    public async Task BindModelAsync_CollectionNotFound_ThrowsNotFoundException()
    {
        var binder = new CollectionModelBinder();
        _collectionRepository.GetByIdAsync(_collectionId).Returns((Collection)null);

        var context = CreateBindingContext();

        await Assert.ThrowsAsync<NotFoundException>(() => binder.BindModelAsync(context));
    }

    [Fact]
    public async Task BindModelAsync_CollectionBelongsToDifferentOrg_ThrowsNotFoundException()
    {
        var binder = new CollectionModelBinder();
        var wrongOrgCollection = new Collection { Id = _collectionId, OrganizationId = Guid.NewGuid() };
        _collectionRepository.GetByIdAsync(_collectionId).Returns(wrongOrgCollection);

        var context = CreateBindingContext();

        await Assert.ThrowsAsync<NotFoundException>(() => binder.BindModelAsync(context));
    }

    [Fact]
    public async Task BindModelAsync_InvalidOrgId_ThrowsBadRequestException()
    {
        var binder = new CollectionModelBinder();
        var context = CreateBindingContext(orgIdRouteValue: "not-a-guid");

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => binder.BindModelAsync(context));
        Assert.Equal("Route parameter 'orgId' or 'organizationId' is missing or invalid.", exception.Message);
    }

    [Fact]
    public async Task BindModelAsync_MissingOrgId_ThrowsBadRequestException()
    {
        var binder = new CollectionModelBinder();
        var context = CreateBindingContext(includeOrgId: false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => binder.BindModelAsync(context));
        Assert.Equal("Route parameter 'orgId' or 'organizationId' is missing or invalid.", exception.Message);
    }

    [Fact]
    public async Task BindModelAsync_InvalidCollectionId_ThrowsBadRequestException()
    {
        var binder = new CollectionModelBinder();
        var context = CreateBindingContext(collectionIdRouteValue: "not-a-guid");

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => binder.BindModelAsync(context));
        Assert.Equal("Route parameter 'id' is missing or invalid.", exception.Message);
    }

    [Fact]
    public async Task BindModelAsync_MissingCollectionId_ThrowsBadRequestException()
    {
        var binder = new CollectionModelBinder();
        var context = CreateBindingContext(includeCollectionId: false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => binder.BindModelAsync(context));
        Assert.Equal("Route parameter 'id' is missing or invalid.", exception.Message);
    }

    [Fact]
    public async Task BindModelAsync_CustomRouteParamName_ReadsCorrectRouteValue()
    {
        var binder = new CollectionModelBinder();
        _collectionRepository.GetByIdAsync(_collectionId).Returns(_collection);

        var parameterInfo = typeof(CollectionModelBinderTests)
            .GetMethod(nameof(DummyMethodWithCustomRouteParam), BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters()[0];

        var context = CreateBindingContext(
            collectionIdRouteKey: "collectionId",
            parameterInfo: parameterInfo);

        await binder.BindModelAsync(context);

        Assert.True(context.Result.IsModelSet);
        Assert.Equal(_collection, context.Result.Model);
    }

    /// <summary>
    /// Dummy method used to produce a <see cref="ParameterInfo"/> carrying a custom
    /// <see cref="InjectCollectionAttribute"/> for the custom route param test.
    /// </summary>
    private static void DummyMethodWithCustomRouteParam([InjectCollection("collectionId")] Collection collection)
    { }

    private DefaultModelBindingContext CreateBindingContext(
        string orgIdRouteValue = null,
        string collectionIdRouteValue = null,
        string collectionIdRouteKey = "id",
        bool includeOrgId = true,
        bool includeCollectionId = true,
        ParameterInfo parameterInfo = null)
    {
        var httpContext = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddScoped(_ => _collectionRepository);
        httpContext.RequestServices = services.BuildServiceProvider();

        var routeData = new RouteData();
        if (includeOrgId)
        {
            routeData.Values["orgId"] = orgIdRouteValue ?? _orgId.ToString();
        }
        if (includeCollectionId)
        {
            routeData.Values[collectionIdRouteKey] = collectionIdRouteValue ?? _collectionId.ToString();
        }

        httpContext.Request.RouteValues = routeData.Values;

        var actionContext = new ActionContext(
            httpContext,
            routeData,
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor(),
            new ModelStateDictionary());

        var metadataProvider = new EmptyModelMetadataProvider();
        ModelMetadata metadata;

        metadata = parameterInfo != null ? metadataProvider.GetMetadataForParameter(parameterInfo) : metadataProvider.GetMetadataForType(typeof(Collection));

        return new DefaultModelBindingContext
        {
            ActionContext = actionContext,
            ModelMetadata = metadata,
            ModelName = "collection"
        };
    }
}
