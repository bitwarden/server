using AutoFixture.Xunit3;
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
}
