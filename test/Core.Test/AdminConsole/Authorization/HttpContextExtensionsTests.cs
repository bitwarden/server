using AutoFixture.Xunit2;
using Bit.Core.AdminConsole.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Authorization;

public class HttpContextExtensionsTests
{

    [Theory]
    [InlineAutoData("orgId")]
    [InlineAutoData("organizationId")]
    public void GetOrganizationId_GivenValidParameter_ReturnsOrganizationId(string paramName, Guid orgId)
    {
        var httpContext = new DefaultHttpContext
        {
            Request =
            {
                RouteValues =
                    new RouteValueDictionary { { "userId", "someGuid" }, { paramName, orgId.ToString() } }
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
            Request =
            {
                RouteValues =
                    new RouteValueDictionary { { "userId", "someGuid" }, { paramName, "invalidGuid" } }
            }
        };

        var exception = Assert.Throws<InvalidOperationException>(() => httpContext.GetOrganizationId());
        Assert.Equal(HttpContextExtensions.NoOrgIdError, exception.Message);
    }
}
