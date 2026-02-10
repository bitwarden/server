#nullable enable

using Bit.Api.Controllers;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Controllers;

public class SsoCookieVendorControllerTests : IDisposable
{
    private readonly SsoCookieVendorController _sut;
    private readonly GlobalSettings _globalSettings;

    public SsoCookieVendorControllerTests()
    {
        _globalSettings = new GlobalSettings
        {
            Communication = new GlobalSettings.CommunicationSettings
            {
                Bootstrap = "ssoCookieVendor",
                SsoCookieVendor = new GlobalSettings.SsoCookieVendorSettings
                {
                    CookieName = "test-cookie"
                }
            }
        };
        _sut = new SsoCookieVendorController(_globalSettings);
    }

    public void Dispose()
    {
        _sut?.Dispose();
    }

    private void MockHttpContextWithCookies(Dictionary<string, string> cookies)
    {
        var httpContext = new DefaultHttpContext();
        var cookieCollection = Substitute.For<IRequestCookieCollection>();

        // Mock the TryGetValue method
        cookieCollection.TryGetValue(Arg.Any<string>(), out Arg.Any<string?>())
            .Returns(callInfo =>
            {
                var key = callInfo.ArgAt<string>(0);
                if (cookies.TryGetValue(key, out var value))
                {
                    callInfo[1] = value;
                    return true;
                }
                callInfo[1] = null;
                return false;
            });

        // Mock the indexer if needed
        cookieCollection[Arg.Any<string>()].Returns(callInfo =>
        {
            var key = callInfo.ArgAt<string>(0);
            return cookies.TryGetValue(key, out var value) ? value : null;
        });

        httpContext.Request.Cookies = cookieCollection;
        _sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("none")]
    public void Get_WhenBootstrapNotConfigured_Returns404(string? bootstrap)
    {
        // Arrange
#nullable disable
        _globalSettings.Communication.Bootstrap = bootstrap;
#nullable restore
        MockHttpContextWithCookies([]);

        // Act
        var result = _sut.Get();

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void Get_WhenCookieNameNotConfigured_Returns500()
    {
        // Arrange
        _globalSettings.Communication.SsoCookieVendor.CookieName = string.Empty;
        MockHttpContextWithCookies([]);

        // Act
        var result = _sut.Get();

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    [Fact]
    public void Get_WhenCookieNameIsEmpty_Returns500()
    {
        // Arrange
        _globalSettings.Communication.SsoCookieVendor.CookieName = "";
        MockHttpContextWithCookies([]);

        // Act
        var result = _sut.Get();

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    [Fact]
    public void Get_WhenSingleCookieExists_ReturnsRedirectWithCorrectUri()
    {
        // Arrange
        var cookies = new Dictionary<string, string>
        {
            { "test-cookie", "my-token-value-123" }
        };
        MockHttpContextWithCookies(cookies);

        // Act
        var result = _sut.Get();

        // Assert
        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal("bitwarden://sso-cookie-vendor?test-cookie=my-token-value-123&d=1", redirectResult.Url);
    }

    [Fact]
    public void Get_WhenSingleCookieHasSpecialCharacters_EncodesCorrectly()
    {
        // Arrange
        var cookies = new Dictionary<string, string>
        {
            { "test-cookie", "value with spaces & special=chars!" }
        };
        MockHttpContextWithCookies(cookies);

        // Act
        var result = _sut.Get();

        // Assert
        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Contains("value%20with%20spaces", redirectResult.Url);
        Assert.Contains("%26", redirectResult.Url); // & encoded
        Assert.Contains("%3D", redirectResult.Url); // = encoded
        Assert.Contains("%21", redirectResult.Url); // ! encoded
    }

    [Fact]
    public void Get_WhenShardedCookiesExist_ReturnsRedirectWithShardedUri()
    {
        // Arrange
        var cookies = new Dictionary<string, string>
        {
            { "test-cookie-0", "part1" },
            { "test-cookie-1", "part2" },
            { "test-cookie-2", "part3" }
        };
        MockHttpContextWithCookies(cookies);

        // Act
        var result = _sut.Get();

        // Assert
        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.StartsWith("bitwarden://sso-cookie-vendor?", redirectResult.Url);
        Assert.Contains("test-cookie-0=part1", redirectResult.Url);
        Assert.Contains("test-cookie-1=part2", redirectResult.Url);
        Assert.Contains("test-cookie-2=part3", redirectResult.Url);
        Assert.EndsWith("d=1", redirectResult.Url);
    }

    [Fact]
    public void Get_WhenShardedCookiesWithGap_StopsAtFirstGap()
    {
        // Arrange
        var cookies = new Dictionary<string, string>
        {
            { "test-cookie-0", "part0" },
            { "test-cookie-1", "part1" },
            // Missing test-cookie-2
            { "test-cookie-3", "part3" },
            { "test-cookie-4", "part4" }
        };
        MockHttpContextWithCookies(cookies);

        // Act
        var result = _sut.Get();

        // Assert
        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Contains("test-cookie-0=part0", redirectResult.Url);
        Assert.Contains("test-cookie-1=part1", redirectResult.Url);
        Assert.DoesNotContain("test-cookie-3", redirectResult.Url);
        Assert.DoesNotContain("test-cookie-4", redirectResult.Url);
        Assert.EndsWith("d=1", redirectResult.Url);
    }

    [Fact]
    public void Get_WhenOnlyGappedShardsExist_Returns404()
    {
        // Arrange - only test-cookie-2 exists, not test-cookie-0 or test-cookie-1
        var cookies = new Dictionary<string, string>
        {
            { "test-cookie-2", "part2" },
            { "test-cookie-3", "part3" }
        };
        MockHttpContextWithCookies(cookies);

        // Act
        var result = _sut.Get();

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void Get_WhenNoCookiesFound_Returns404()
    {
        // Arrange
        MockHttpContextWithCookies([]);

        // Act
        var result = _sut.Get();

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void Get_WhenUnrelatedCookiesExist_Returns404()
    {
        // Arrange
        var cookies = new Dictionary<string, string>
        {
            { "other-cookie", "value" },
            { "another-cookie", "value2" }
        };
        MockHttpContextWithCookies(cookies);

        // Act
        var result = _sut.Get();

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void Get_WhenUriExceedsMaxLength_Returns400()
    {
        // Arrange - create a very long cookie value that will exceed 8192 characters
        // URI format: "bitwarden://sso-cookie-vendor?test-cookie={value}"
        // Base URI length is about 43 characters, so we need value > 8149
        var longValue = new string('a', 8200);
        var cookies = new Dictionary<string, string>
        {
            { "test-cookie", longValue }
        };
        MockHttpContextWithCookies(cookies);

        // Act
        var result = _sut.Get();

        // Assert
        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public void Get_WhenSingleCookiePreferredOverSharded_ReturnsSingleCookie()
    {
        // Arrange - both single and sharded cookies exist
        var cookies = new Dictionary<string, string>
        {
            { "test-cookie", "single-value" },
            { "test-cookie-0", "shard0" },
            { "test-cookie-1", "shard1" }
        };
        MockHttpContextWithCookies(cookies);

        // Act
        var result = _sut.Get();

        // Assert
        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal("bitwarden://sso-cookie-vendor?test-cookie=single-value&d=1", redirectResult.Url);
    }

    [Fact]
    public void Get_WhenEmptyCookieValue_TreatsAsNotFound()
    {
        // Arrange
        var cookies = new Dictionary<string, string>
        {
            { "test-cookie", "" }
        };
        MockHttpContextWithCookies(cookies);

        // Act
        var result = _sut.Get();

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void Get_WhenShardedCookiesHaveMaxCount_ProcessesAllShards()
    {
        // Arrange - create 20 sharded cookies (MaxShardCount)
        var cookies = new Dictionary<string, string>();
        for (var i = 0; i < 20; i++)
        {
            cookies[$"test-cookie-{i}"] = $"part{i}";
        }
        MockHttpContextWithCookies(cookies);

        // Act
        var result = _sut.Get();

        // Assert
        var redirectResult = Assert.IsType<RedirectResult>(result);
        for (var i = 0; i < 20; i++)
        {
            Assert.Contains($"test-cookie-{i}=part{i}", redirectResult.Url);
        }
        Assert.EndsWith("d=1", redirectResult.Url);
    }

    [Fact]
    public void Get_WhenShardedCookiesExceedMaxCount_OnlyProcessesFirst20()
    {
        // Arrange - create 25 sharded cookies (more than MaxShardCount of 20)
        var cookies = new Dictionary<string, string>();
        for (var i = 0; i < 25; i++)
        {
            cookies[$"test-cookie-{i}"] = $"part{i}";
        }
        MockHttpContextWithCookies(cookies);

        // Act
        var result = _sut.Get();

        // Assert
        var redirectResult = Assert.IsType<RedirectResult>(result);
        // Should contain first 20
        for (var i = 0; i < 20; i++)
        {
            Assert.Contains($"test-cookie-{i}=part{i}", redirectResult.Url);
        }
        // Should NOT contain 21-25
        for (var i = 20; i < 25; i++)
        {
            Assert.DoesNotContain($"test-cookie-{i}=part{i}", redirectResult.Url);
        }
    }
}
