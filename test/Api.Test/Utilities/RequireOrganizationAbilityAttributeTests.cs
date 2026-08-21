using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Utilities;

public class RequireOrganizationAbilityAttributeTests
{
    private const string _abilityKey = nameof(OrganizationAbility.UseRiskInsights);

    [Theory]
    [InlineData("orgId")]
    [InlineData("organizationId")]
    public async Task OnActionExecutionAsync_AbilityEnabled_InvokesNext(string routeParameterName)
    {
        var orgId = Guid.NewGuid();
        var cacheService = CreateCacheService(orgId, new OrganizationAbility { Id = orgId, UseRiskInsights = true });
        var context = CreateContext(cacheService, new RouteValueDictionary { { routeParameterName, orgId.ToString() } });
        var sut = new RequireOrganizationAbilityAttribute(_abilityKey);
        var nextCallCount = 0;

        await sut.OnActionExecutionAsync(context, () =>
        {
            nextCallCount++;
            return Task.FromResult(CreateExecutedContext(context));
        });

        Assert.Equal(1, nextCallCount);
        await cacheService.Received(1).GetOrganizationAbilityAsync(orgId);
    }

    [Theory]
    [InlineData(nameof(OrganizationAbility.UseRiskInsights))]
    [InlineData(nameof(OrganizationAbility.UseEvents))]
    [InlineData(nameof(OrganizationAbility.UseSso))]
    [InlineData(nameof(OrganizationAbility.Enabled))]
    public async Task OnActionExecutionAsync_AnyBooleanAbility_IsResolvedByName(string abilityKey)
    {
        var orgId = Guid.NewGuid();
        var ability = new OrganizationAbility { Id = orgId };
        typeof(OrganizationAbility).GetProperty(abilityKey)!.SetValue(ability, true);

        var cacheService = CreateCacheService(orgId, ability);
        var context = CreateContext(cacheService, new RouteValueDictionary { { "organizationId", orgId.ToString() } });
        var sut = new RequireOrganizationAbilityAttribute(abilityKey);
        var nextCalled = false;

        await sut.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(CreateExecutedContext(context));
        });

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task OnActionExecutionAsync_AbilityDisabled_ThrowsBadRequestExceptionAndDoesNotInvokeNext()
    {
        var orgId = Guid.NewGuid();
        var cacheService = CreateCacheService(orgId, new OrganizationAbility { Id = orgId, UseRiskInsights = false });
        var context = CreateContext(cacheService, new RouteValueDictionary { { "organizationId", orgId.ToString() } });
        var sut = new RequireOrganizationAbilityAttribute(_abilityKey);
        var nextCalled = false;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.OnActionExecutionAsync(context, () =>
            {
                nextCalled = true;
                return Task.FromResult(CreateExecutedContext(context));
            }));

        Assert.Contains("does not have access to this feature", exception.Message);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task OnActionExecutionAsync_OrganizationAbilityNotFound_ThrowsBadRequestExceptionAndDoesNotInvokeNext()
    {
        var orgId = Guid.NewGuid();
        var cacheService = CreateCacheService(orgId, null);
        var context = CreateContext(cacheService, new RouteValueDictionary { { "organizationId", orgId.ToString() } });
        var sut = new RequireOrganizationAbilityAttribute(_abilityKey);
        var nextCalled = false;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.OnActionExecutionAsync(context, () =>
            {
                nextCalled = true;
                return Task.FromResult(CreateExecutedContext(context));
            }));

        Assert.Contains("does not have access to this feature", exception.Message);
        Assert.False(nextCalled);
    }

    [Theory]
    [InlineData("NotARealAbility")]
    [InlineData("useRiskInsights")] // property lookup is case-sensitive
    [InlineData("Id")] // exists, but is not a boolean
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_UnknownAbilityKey_Throws(string? abilityKey)
    {
        var exception = Assert.Throws<ArgumentException>(() => new RequireOrganizationAbilityAttribute(abilityKey!));

        Assert.Equal("abilityKey", exception.ParamName);
        Assert.Contains("must be a valid boolean property", exception.Message);
    }

    [Fact]
    public async Task OnActionExecutionAsync_EmptyOrganizationIdRouteValue_ThrowsAndDoesNotQueryCache()
    {
        var cacheService = Substitute.For<IOrganizationAbilityCacheService>();
        var context = CreateContext(cacheService,
            new RouteValueDictionary { { "organizationId", Guid.Empty.ToString() } });
        var sut = new RequireOrganizationAbilityAttribute(_abilityKey);

        var exception = await Assert.ThrowsAsync<Exception>(() =>
            sut.OnActionExecutionAsync(context, () => Task.FromResult(CreateExecutedContext(context))));

        Assert.Contains("is missing or invalid", exception.Message);
        await cacheService.DidNotReceiveWithAnyArgs().GetOrganizationAbilityAsync(default);
    }

    [Theory]
    [InlineData("someOtherRouteParameter", "d3b07384-d9a0-4f1c-9f4e-3a2b1c0d9e8f")]
    [InlineData("organizationId", "not-a-guid")]
    [InlineData("orgId", "")]
    public async Task OnActionExecutionAsync_MissingOrInvalidOrganizationIdRouteValue_Throws(
        string routeParameterName, string routeParameterValue)
    {
        var cacheService = Substitute.For<IOrganizationAbilityCacheService>();
        var context = CreateContext(cacheService,
            new RouteValueDictionary { { routeParameterName, routeParameterValue } });
        var sut = new RequireOrganizationAbilityAttribute(_abilityKey);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.OnActionExecutionAsync(context, () => Task.FromResult(CreateExecutedContext(context))));

        await cacheService.DidNotReceiveWithAnyArgs().GetOrganizationAbilityAsync(default);
    }

    private static IOrganizationAbilityCacheService CreateCacheService(Guid orgId, OrganizationAbility? ability)
    {
        var cacheService = Substitute.For<IOrganizationAbilityCacheService>();
        cacheService.GetOrganizationAbilityAsync(orgId).Returns(ability);
        return cacheService;
    }

    private static ActionExecutingContext CreateContext(
        IOrganizationAbilityCacheService cacheService,
        RouteValueDictionary routeValues)
    {
        var services = new ServiceCollection();
        services.AddSingleton(cacheService);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            Request = { RouteValues = routeValues }
        };

        var actionContext = new ActionContext(httpContext, new RouteData(routeValues), new ActionDescriptor());

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: new object());
    }

    private static ActionExecutedContext CreateExecutedContext(ActionExecutingContext context) =>
        new(context, context.Filters, context.Controller);
}
