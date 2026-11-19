using AutoFixture.Xunit2;
using Bit.Api.AdminConsole.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization;

public class HttpContextExtensionsTests
{
    [Fact]
    public async Task WithFeaturesCacheAsync_OnlyExecutesCallbackOnce()
    {
        var httpContext = new DefaultHttpContext();
        var callback = Substitute.For<Func<Task<string>>>();
        callback().Returns(Task.FromResult("hello world"));

        // Call once
        var result1 = await httpContext.WithFeaturesCacheAsync(callback);
        Assert.Equal("hello world", result1);
        await callback.ReceivedWithAnyArgs(1).Invoke();

        // Call again - callback not executed again
        var result2 = await httpContext.WithFeaturesCacheAsync(callback);
        Assert.Equal("hello world", result2);
        await callback.ReceivedWithAnyArgs(1).Invoke();
    }

    [Theory]
    [InlineAutoData("orgId")]
    [InlineAutoData("organizationId")]
    public void GetOrganizationId_GivenValidParameter_ReturnsOrganizationId(string paramName, Guid orgId)
    {
        var httpContext = new DefaultHttpContext
        {
            Request = { RouteValues = new RouteValueDictionary
            {
                { "userId", "someGuid" },
                { paramName, orgId.ToString() }
            }
        }
        };

        var result = httpContext.GetOrganizationId();
        Assert.Equal(orgId, result);
    }

    [Theory]
    [InlineAutoData("orgId")]
    [InlineAutoData("organizationId")]
    [InlineAutoData("missingParameter")]
    public void GetOrganizationId_GivenMissingOrInvalidGuid_Throws(string paramName)
    {
        var httpContext = new DefaultHttpContext
        {
            Request = { RouteValues = new RouteValueDictionary
            {
                { "userId", "someGuid" },
                { paramName, "invalidGuid" }
            }
        }
        };

        var exception = Assert.Throws<InvalidOperationException>(() => httpContext.GetOrganizationId());
        Assert.Equal(HttpContextExtensions.NoOrgIdError, exception.Message);
    }

    [Theory]
    [InlineAutoData("orgId")]
    [InlineAutoData("organizationId")]
    public void TryGetOrganizationId_GivenValidParameter_ReturnsOrganizationId(string paramName, Guid orgId)
    {
        var httpContext = new DefaultHttpContext
        {
            Request = { RouteValues = new RouteValueDictionary { { paramName, orgId.ToString() } } }
        };

        Assert.Equal(orgId, httpContext.TryGetOrganizationId());
        Assert.True(httpContext.HasOrganizationIdRouteValue());
    }

    [Theory]
    [InlineData("orgId")]
    [InlineData("organizationId")]
    public void TryGetOrganizationId_GivenInvalidGuid_ReturnsNullButHasRouteValue(string paramName)
    {
        var httpContext = new DefaultHttpContext
        {
            Request = { RouteValues = new RouteValueDictionary { { paramName, "notaguid" } } }
        };

        Assert.Null(httpContext.TryGetOrganizationId());
        Assert.True(httpContext.HasOrganizationIdRouteValue());
    }

    [Fact]
    public void TryGetOrganizationId_GivenNoParameter_ReturnsNullAndHasNoRouteValue()
    {
        var httpContext = new DefaultHttpContext
        {
            Request = { RouteValues = new RouteValueDictionary { { "userId", "someGuid" }, { "orgId", null } } }
        };

        Assert.Null(httpContext.TryGetOrganizationId());
        Assert.False(httpContext.HasOrganizationIdRouteValue());
    }
}
