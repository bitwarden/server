using Bit.Core.Auth.IdentityServer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.IdentityServer;

public class TokenRetrievalTests
{
    private readonly Func<HttpRequest, string> _sut = TokenRetrieval.FromAuthorizationHeaderOrQueryString();

    [Fact]
    public void RetrieveToken_FromHeader_ReturnsToken()
    {
        // Arrange
        var headers = new HeaderDictionary
        {
            { "Authorization", "Bearer test_value" },
            { "X-Test-Header", "random_value" }
        };

        var request = Substitute.For<HttpRequest>();

        request.Headers.Returns(headers);

        // Act
        var token = _sut(request);

        // Assert
        Assert.Equal("test_value", token);
    }

    [Fact]
    public void RetrieveToken_FromQueryString_ReturnsToken()
    {
        // Arrange
        var queryString = new Dictionary<string, StringValues>
        {
            { "access_token", "test_value" },
            { "test-query", "random_value" }
        };

        var request = Substitute.For<HttpRequest>();
        request.Query.Returns(new QueryCollection(queryString));

        // Act
        var token = _sut(request);

        // Assert
        Assert.Equal("test_value", token);
    }

    [Fact]
    public void RetrieveToken_HasBoth_ReturnsHeaderToken()
    {
        // Arrange
        var queryString = new Dictionary<string, StringValues>
        {
            { "access_token", "query_string_token" },
            { "test-query", "random_value" }
        };

        var headers = new HeaderDictionary
        {
            { "Authorization", "Bearer header_token" },
            { "X-Test-Header", "random_value" }
        };

        var request = Substitute.For<HttpRequest>();
        request.Headers.Returns(headers);
        request.Query.Returns(new QueryCollection(queryString));

        // Act
        var token = _sut(request);

        // Assert
        Assert.Equal("header_token", token);
    }

    [Fact]
    public void RetrieveToken_NoToken_ReturnsNull()
    {
        // Arrange
        var request = Substitute.For<HttpRequest>();

        // Act
        var token = _sut(request);

        // Assert
        Assert.Null(token);
    }
}
