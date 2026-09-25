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

public class OrganizationUserModelBinderTests
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly OrganizationUser _organizationUser;
    private readonly Guid _orgId;
    private readonly Guid _orgUserId;

    public OrganizationUserModelBinderTests()
    {
        _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        _orgId = Guid.NewGuid();
        _orgUserId = Guid.NewGuid();
        _organizationUser = new OrganizationUser { Id = _orgUserId, OrganizationId = _orgId };
    }

    [Fact]
    public async Task BindModelAsync_OrgUserExistsAndBelongsToOrg_BindsSuccessfully()
    {
        var binder = new OrganizationUserModelBinder();
        _organizationUserRepository.GetByIdAsync(_orgUserId).Returns(_organizationUser);

        var context = CreateBindingContext();

        await binder.BindModelAsync(context);

        Assert.True(context.Result.IsModelSet);
        Assert.Equal(_organizationUser, context.Result.Model);
    }

    [Fact]
    public async Task BindModelAsync_OrgUserNotFound_ThrowsNotFoundException()
    {
        var binder = new OrganizationUserModelBinder();
        _organizationUserRepository.GetByIdAsync(_orgUserId).Returns((OrganizationUser)null);

        var context = CreateBindingContext();

        await Assert.ThrowsAsync<NotFoundException>(() => binder.BindModelAsync(context));
    }

    [Fact]
    public async Task BindModelAsync_OrgUserBelongsToDifferentOrg_ThrowsNotFoundException()
    {
        var binder = new OrganizationUserModelBinder();
        var wrongOrgUser = new OrganizationUser { Id = _orgUserId, OrganizationId = Guid.NewGuid() };
        _organizationUserRepository.GetByIdAsync(_orgUserId).Returns(wrongOrgUser);

        var context = CreateBindingContext();

        await Assert.ThrowsAsync<NotFoundException>(() => binder.BindModelAsync(context));
    }

    [Fact]
    public async Task BindModelAsync_InvalidOrgId_ThrowsBadRequestException()
    {
        var binder = new OrganizationUserModelBinder();
        var context = CreateBindingContext(orgIdRouteValue: "not-a-guid");

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => binder.BindModelAsync(context));
        Assert.Equal("Route parameter 'orgId' or 'organizationId' is missing or invalid.", exception.Message);
    }

    [Fact]
    public async Task BindModelAsync_MissingOrgId_ThrowsBadRequestException()
    {
        var binder = new OrganizationUserModelBinder();
        var context = CreateBindingContext(includeOrgId: false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => binder.BindModelAsync(context));
        Assert.Equal("Route parameter 'orgId' or 'organizationId' is missing or invalid.", exception.Message);
    }

    [Fact]
    public async Task BindModelAsync_InvalidOrgUserId_ThrowsBadRequestException()
    {
        var binder = new OrganizationUserModelBinder();
        var context = CreateBindingContext(orgUserIdRouteValue: "not-a-guid");

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => binder.BindModelAsync(context));
        Assert.Equal("Route parameter 'id' is missing or invalid.", exception.Message);
    }

    [Fact]
    public async Task BindModelAsync_MissingOrgUserId_ThrowsBadRequestException()
    {
        var binder = new OrganizationUserModelBinder();
        var context = CreateBindingContext(includeOrgUserId: false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => binder.BindModelAsync(context));
        Assert.Equal("Route parameter 'id' is missing or invalid.", exception.Message);
    }

    [Fact]
    public async Task BindModelAsync_OrganizationIdRouteParam_ResolvesOrgId()
    {
        var binder = new OrganizationUserModelBinder();
        _organizationUserRepository.GetByIdAsync(_orgUserId).Returns(_organizationUser);

        var context = CreateBindingContext(useOrganizationIdRoute: true);

        await binder.BindModelAsync(context);

        Assert.True(context.Result.IsModelSet);
        Assert.Equal(_organizationUser, context.Result.Model);
    }

    [Fact]
    public async Task BindModelAsync_CustomRouteParamName_ReadsCorrectRouteValue()
    {
        var binder = new OrganizationUserModelBinder();
        _organizationUserRepository.GetByIdAsync(_orgUserId).Returns(_organizationUser);

        var parameterInfo = typeof(OrganizationUserModelBinderTests)
            .GetMethod(nameof(DummyMethodWithCustomRouteParam), BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters()[0];

        var context = CreateBindingContext(
            orgUserIdRouteKey: "organizationUserId",
            parameterInfo: parameterInfo);

        await binder.BindModelAsync(context);

        Assert.True(context.Result.IsModelSet);
        Assert.Equal(_organizationUser, context.Result.Model);
    }

    /// <summary>
    /// Dummy method used to produce a <see cref="ParameterInfo"/> carrying a custom
    /// <see cref="InjectOrganizationUserAttribute"/> for the custom route param test.
    /// </summary>
    private static void DummyMethodWithCustomRouteParam(
        [InjectOrganizationUser("organizationUserId")] OrganizationUser user)
    { }

    private DefaultModelBindingContext CreateBindingContext(
        string orgIdRouteValue = null,
        string orgUserIdRouteValue = null,
        string orgUserIdRouteKey = "id",
        bool includeOrgId = true,
        bool includeOrgUserId = true,
        bool useOrganizationIdRoute = false,
        ParameterInfo parameterInfo = null)
    {
        var httpContext = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddScoped(_ => _organizationUserRepository);
        httpContext.RequestServices = services.BuildServiceProvider();

        var routeData = new RouteData();
        if (includeOrgId)
        {
            var key = useOrganizationIdRoute ? "organizationId" : "orgId";
            routeData.Values[key] = orgIdRouteValue ?? _orgId.ToString();
        }
        if (includeOrgUserId)
        {
            routeData.Values[orgUserIdRouteKey] = orgUserIdRouteValue ?? _orgUserId.ToString();
        }

        httpContext.Request.RouteValues = routeData.Values;

        var actionContext = new ActionContext(
            httpContext,
            routeData,
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor(),
            new ModelStateDictionary());

        var metadataProvider = new EmptyModelMetadataProvider();
        ModelMetadata metadata;

        if (parameterInfo != null)
        {
            metadata = metadataProvider.GetMetadataForParameter(parameterInfo);
        }
        else
        {
            metadata = metadataProvider.GetMetadataForType(typeof(OrganizationUser));
        }

        return new DefaultModelBindingContext
        {
            ActionContext = actionContext,
            ModelMetadata = metadata,
            ModelName = "organizationUser"
        };
    }
}
