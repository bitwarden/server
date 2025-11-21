using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class PlayIdServiceTests
{
    [Theory]
    [BitAutoData]
    public void InPlay_WhenPlayIdSetAndDevelopment_ReturnsTrue(
        string playId,
        SutProvider<PlayIdService> sutProvider)
    {
        sutProvider.GetDependency<IHostEnvironment>().EnvironmentName.Returns(Environments.Development);
        sutProvider.Sut.PlayId = playId;

        var result = sutProvider.Sut.InPlay(out var resultPlayId);

        Assert.True(result);
        Assert.Equal(playId, resultPlayId);
    }

    [Theory]
    [BitAutoData]
    public void InPlay_WhenPlayIdSetButNotDevelopment_ReturnsFalse(
        string playId,
        SutProvider<PlayIdService> sutProvider)
    {
        sutProvider.GetDependency<IHostEnvironment>().EnvironmentName.Returns(Environments.Production);
        sutProvider.Sut.PlayId = playId;

        var result = sutProvider.Sut.InPlay(out var resultPlayId);

        Assert.False(result);
        Assert.Empty(resultPlayId);
    }

    [Theory]
    [BitAutoData((string?)null)]
    [BitAutoData("")]
    public void InPlay_WhenPlayIdNullOrEmptyAndDevelopment_ReturnsFalse(
        string? playId,
        SutProvider<PlayIdService> sutProvider)
    {
        sutProvider.GetDependency<IHostEnvironment>().EnvironmentName.Returns(Environments.Development);
        sutProvider.Sut.PlayId = playId;

        var result = sutProvider.Sut.InPlay(out var resultPlayId);

        Assert.False(result);
        Assert.Empty(resultPlayId);
    }

    [Theory]
    [BitAutoData]
    public void PlayId_CanGetAndSet(string playId)
    {
        var hostEnvironment = Substitute.For<IHostEnvironment>();
        var sut = new PlayIdService(hostEnvironment);

        sut.PlayId = playId;

        Assert.Equal(playId, sut.PlayId);
    }
}

public class NeverPlayIdServicesTests
{
    [Fact]
    public void InPlay_ReturnsFalse()
    {
        var sut = new NeverPlayIdServices();

        var result = sut.InPlay(out var playId);

        Assert.False(result);
        Assert.Empty(playId);
    }

    [Theory]
    [InlineData("test-play-id")]
    [InlineData(null)]
    public void PlayId_SetterDoesNothing_GetterReturnsNull(string? value)
    {
        var sut = new NeverPlayIdServices();

        sut.PlayId = value;

        Assert.Null(sut.PlayId);
    }
}

public class PlayIdSingletonServiceTests
{
    [Theory]
    [BitAutoData]
    public void InPlay_WhenNoHttpContext_ReturnsFalse(
        SutProvider<PlayIdSingletonService> sutProvider)
    {
        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext.Returns((HttpContext?)null);
        sutProvider.GetDependency<IHostEnvironment>().EnvironmentName.Returns(Environments.Development);

        var result = sutProvider.Sut.InPlay(out var playId);

        Assert.False(result);
        Assert.Empty(playId);
    }

    [Theory]
    [BitAutoData]
    public void InPlay_WhenNotDevelopment_ReturnsFalse(
        string playIdValue,
        SutProvider<PlayIdSingletonService> sutProvider)
    {
        var httpContext = Substitute.For<HttpContext>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        var scopedPlayIdService = Substitute.For<PlayIdService>(Substitute.For<IHostEnvironment>());
        scopedPlayIdService.PlayId = playIdValue;
        scopedPlayIdService.InPlay(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = playIdValue;
            return true;
        });

        httpContext.RequestServices.Returns(serviceProvider);
        serviceProvider.GetRequiredService<PlayIdService>().Returns(scopedPlayIdService);

        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext.Returns(httpContext);
        sutProvider.GetDependency<IHostEnvironment>().EnvironmentName.Returns(Environments.Production);

        var result = sutProvider.Sut.InPlay(out var playId);

        Assert.False(result);
        Assert.Empty(playId);
    }

    [Theory]
    [BitAutoData]
    public void InPlay_WhenDevelopmentAndHttpContextWithPlayId_ReturnsTrue(
        string playIdValue,
        SutProvider<PlayIdSingletonService> sutProvider)
    {
        var httpContext = Substitute.For<HttpContext>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        var hostEnvironment = Substitute.For<IHostEnvironment>();
        hostEnvironment.EnvironmentName.Returns(Environments.Development);
        var scopedPlayIdService = new PlayIdService(hostEnvironment) { PlayId = playIdValue };

        httpContext.RequestServices.Returns(serviceProvider);
        serviceProvider.GetRequiredService<PlayIdService>().Returns(scopedPlayIdService);

        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext.Returns(httpContext);
        sutProvider.GetDependency<IHostEnvironment>().EnvironmentName.Returns(Environments.Development);

        var result = sutProvider.Sut.InPlay(out var playId);

        Assert.True(result);
        Assert.Equal(playIdValue, playId);
    }

    [Theory]
    [BitAutoData]
    public void PlayId_GetterRetrievesFromScopedService(
        string playIdValue,
        SutProvider<PlayIdSingletonService> sutProvider)
    {
        var httpContext = Substitute.For<HttpContext>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        var hostEnvironment = Substitute.For<IHostEnvironment>();
        var scopedPlayIdService = new PlayIdService(hostEnvironment) { PlayId = playIdValue };

        httpContext.RequestServices.Returns(serviceProvider);
        serviceProvider.GetRequiredService<PlayIdService>().Returns(scopedPlayIdService);

        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext.Returns(httpContext);

        var result = sutProvider.Sut.PlayId;

        Assert.Equal(playIdValue, result);
    }

    [Theory]
    [BitAutoData]
    public void PlayId_SetterSetsOnScopedService(
        string playIdValue,
        SutProvider<PlayIdSingletonService> sutProvider)
    {
        var httpContext = Substitute.For<HttpContext>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        var hostEnvironment = Substitute.For<IHostEnvironment>();
        var scopedPlayIdService = new PlayIdService(hostEnvironment);

        httpContext.RequestServices.Returns(serviceProvider);
        serviceProvider.GetRequiredService<PlayIdService>().Returns(scopedPlayIdService);

        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext.Returns(httpContext);

        sutProvider.Sut.PlayId = playIdValue;

        Assert.Equal(playIdValue, scopedPlayIdService.PlayId);
    }

    [Theory]
    [BitAutoData]
    public void PlayId_WhenNoHttpContext_GetterReturnsNull(
        SutProvider<PlayIdSingletonService> sutProvider)
    {
        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext.Returns((HttpContext?)null);

        var result = sutProvider.Sut.PlayId;

        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public void PlayId_WhenNoHttpContext_SetterDoesNotThrow(
        string playIdValue,
        SutProvider<PlayIdSingletonService> sutProvider)
    {
        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext.Returns((HttpContext?)null);

        sutProvider.Sut.PlayId = playIdValue;
    }
}
