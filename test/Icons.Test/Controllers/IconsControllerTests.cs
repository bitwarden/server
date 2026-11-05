using Bit.Icons.Controllers;
using Bit.Icons.Models;
using Bit.Icons.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Icons.Test.Controllers;

public class IconsControllerTests
{
    private const string _hostname = "example.com";

    private readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100_000 });
    private readonly IDomainMappingService _domainMappingService = Substitute.For<IDomainMappingService>();
    private readonly IIconFetchingService _iconFetchingService = Substitute.For<IIconFetchingService>();
    private readonly IconsSettings _settings = new()
    {
        CacheEnabled = true,
        CacheHours = 1,
        CacheSizeLimit = 100_000
    };

    public IconsControllerTests()
    {
        _domainMappingService.MapDomain(Arg.Any<string>()).Returns(ci => ci.Arg<string>());
    }

    private IconsController CreateSut() =>
        new(_memoryCache, _domainMappingService, _iconFetchingService,
            Substitute.For<ILogger<IconsController>>(), _settings);

    [Fact]
    public async Task Get_WhenIconIsFound_ReturnsIcon()
    {
        var icon = new Icon { Image = [1, 2, 3], Format = "image/png" };
        _iconFetchingService.GetIconAsync(_hostname).Returns(icon);

        var result = await CreateSut().Get(_hostname);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal(icon.Image, file.FileContents);
    }

    [Fact]
    public async Task Get_WhenIconIsNotFound_ReturnsNotFound()
    {
        _iconFetchingService.GetIconAsync(_hostname).Returns((Icon)null);

        var result = await CreateSut().Get(_hostname);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Get_WhenIconIsNotFoundAndCached_ReturnsNotFound()
    {
        _iconFetchingService.GetIconAsync(_hostname).Returns((Icon)null);
        var sut = CreateSut();

        await sut.Get(_hostname);
        var result = await sut.Get(_hostname);

        Assert.IsType<NotFoundResult>(result);
        await _iconFetchingService.Received(1).GetIconAsync(_hostname);
    }
}
