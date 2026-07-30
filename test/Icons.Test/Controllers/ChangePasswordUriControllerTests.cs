using Bit.Icons.Controllers;
using Bit.Icons.Models;
using Bit.Icons.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Icons.Test.Controllers;

public class ChangePasswordUriControllerTests
{
    private const string _uri = "example.com";

    private readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100_000 });
    private readonly IDomainMappingService _domainMappingService = Substitute.For<IDomainMappingService>();
    private readonly IChangePasswordUriService _changePasswordService = Substitute.For<IChangePasswordUriService>();
    private readonly ChangePasswordUriSettings _settings = new()
    {
        CacheEnabled = true,
        CacheHours = 1,
        CacheSizeLimit = 100_000
    };
    private readonly ILogger<ChangePasswordUriController> _logger = Substitute.For<ILogger<ChangePasswordUriController>>();

    public ChangePasswordUriControllerTests()
    {
        _domainMappingService.MapDomain(Arg.Any<string>()).Returns(ci => ci.Arg<string>());
    }

    private ChangePasswordUriController CreateSut() =>
        new(_memoryCache, _domainMappingService, _changePasswordService, _settings, _logger)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    [Fact]
    public async Task Get_WhenLookupFails_DoesNotCacheAndReprobes()
    {
        _changePasswordService.GetChangePasswordUri(_uri).Returns(ChangePasswordUriResult.LookupFailed);
        var sut = CreateSut();

        var first = await sut.Get(_uri);
        var second = await sut.Get(_uri);

        // A failed lookup must not be cached, so every request re-probes the service.
        await _changePasswordService.Received(2).GetChangePasswordUri(_uri);
        Assert.Null(GetResponseUri(first));
        Assert.Null(GetResponseUri(second));
    }

    [Fact]
    public async Task Get_WhenLookupFails_SetsNoStore()
    {
        _changePasswordService.GetChangePasswordUri(_uri).Returns(ChangePasswordUriResult.LookupFailed);
        var sut = CreateSut();

        await sut.Get(_uri);

        var cacheControl = sut.Response.GetTypedHeaders().CacheControl;
        Assert.NotNull(cacheControl);
        Assert.True(cacheControl!.NoStore);
        Assert.False(cacheControl.Public);
    }

    [Fact]
    public async Task Get_WhenFound_CachesResultAndSkipsSecondProbe()
    {
        _changePasswordService.GetChangePasswordUri(_uri)
            .Returns(ChangePasswordUriResult.Found("https://example.com/.well-known/change-password"));
        var sut = CreateSut();

        var first = await sut.Get(_uri);
        var second = await sut.Get(_uri);

        // A definitive answer is cached, so the service is only probed once.
        await _changePasswordService.Received(1).GetChangePasswordUri(_uri);
        Assert.Equal("https://example.com/.well-known/change-password", GetResponseUri(first));
        Assert.Equal("https://example.com/.well-known/change-password", GetResponseUri(second));
    }

    [Fact]
    public async Task Get_WhenNotSupported_CachesNullAndSkipsSecondProbe()
    {
        _changePasswordService.GetChangePasswordUri(_uri).Returns(ChangePasswordUriResult.NotSupported);
        var sut = CreateSut();

        var first = await sut.Get(_uri);
        var second = await sut.Get(_uri);

        // A confirmed "not supported" is a definitive answer and is cached.
        await _changePasswordService.Received(1).GetChangePasswordUri(_uri);
        Assert.Null(GetResponseUri(first));
        Assert.Null(GetResponseUri(second));
    }

    [Fact]
    public async Task Get_WhenDefinitive_SetsShortPublicCache()
    {
        _changePasswordService.GetChangePasswordUri(_uri).Returns(ChangePasswordUriResult.NotSupported);
        var sut = CreateSut();

        await sut.Get(_uri);

        var cacheControl = sut.Response.GetTypedHeaders().CacheControl;
        Assert.NotNull(cacheControl);
        Assert.True(cacheControl!.Public);
        Assert.False(cacheControl.NoStore);
        Assert.Equal(TimeSpan.FromHours(1), cacheControl.MaxAge);
    }

    private static string? GetResponseUri(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ChangePasswordUriResponse>(ok.Value);
        return response.uri;
    }
}
