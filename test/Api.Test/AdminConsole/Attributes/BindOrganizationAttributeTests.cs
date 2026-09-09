using Bit.Api.AdminConsole.Attributes;
using Bit.Core.AdminConsole.Entities;
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

public class BindOrganizationAttributeTests
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly Organization _organization;
    private readonly Guid _orgId;

    public BindOrganizationAttributeTests()
    {
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _orgId = Guid.NewGuid();
        _organization = new Organization { Id = _orgId };
    }

    [Fact]
    public async Task BindModelAsync_OrganizationExists_BindsSuccessfully()
    {
        var binder = new OrganizationModelBinder();
        _organizationRepository.GetByIdAsync(_orgId).Returns(_organization);

        var context = CreateBindingContext();

        await binder.BindModelAsync(context);

        Assert.True(context.Result.IsModelSet);
        Assert.Equal(_organization, context.Result.Model);
    }

    [Fact]
    public async Task BindModelAsync_OrganizationNotFound_ThrowsNotFoundException()
    {
        var binder = new OrganizationModelBinder();
        _organizationRepository.GetByIdAsync(_orgId).Returns((Organization)null);

        var context = CreateBindingContext();

        await Assert.ThrowsAsync<NotFoundException>(() => binder.BindModelAsync(context));
    }

    [Fact]
    public async Task BindModelAsync_InvalidOrgId_ThrowsBadRequestException()
    {
        var binder = new OrganizationModelBinder();
        var context = CreateBindingContext(orgIdRouteValue: "not-a-guid");

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => binder.BindModelAsync(context));
        Assert.Equal("Route parameter 'orgId' or 'organizationId' is missing or invalid.", exception.Message);
    }

    [Fact]
    public async Task BindModelAsync_MissingOrgId_ThrowsBadRequestException()
    {
        var binder = new OrganizationModelBinder();
        var context = CreateBindingContext(includeOrgId: false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => binder.BindModelAsync(context));
        Assert.Equal("Route parameter 'orgId' or 'organizationId' is missing or invalid.", exception.Message);
    }

    [Fact]
    public async Task BindModelAsync_OrganizationIdRouteParam_ResolvesOrgId()
    {
        var binder = new OrganizationModelBinder();
        _organizationRepository.GetByIdAsync(_orgId).Returns(_organization);

        var context = CreateBindingContext(useOrganizationIdRoute: true);

        await binder.BindModelAsync(context);

        Assert.True(context.Result.IsModelSet);
        Assert.Equal(_organization, context.Result.Model);
    }

    private DefaultModelBindingContext CreateBindingContext(
        string orgIdRouteValue = null,
        bool includeOrgId = true,
        bool useOrganizationIdRoute = false)
    {
        var httpContext = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddScoped(_ => _organizationRepository);
        httpContext.RequestServices = services.BuildServiceProvider();

        var routeData = new RouteData();
        if (includeOrgId)
        {
            var key = useOrganizationIdRoute ? "organizationId" : "orgId";
            routeData.Values[key] = orgIdRouteValue ?? _orgId.ToString();
        }

        httpContext.Request.RouteValues = routeData.Values;

        var actionContext = new ActionContext(
            httpContext,
            routeData,
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor(),
            new ModelStateDictionary());

        var metadataProvider = new EmptyModelMetadataProvider();
        var metadata = metadataProvider.GetMetadataForType(typeof(Organization));

        return new DefaultModelBindingContext
        {
            ActionContext = actionContext,
            ModelMetadata = metadata,
            ModelName = "organization"
        };
    }
}
