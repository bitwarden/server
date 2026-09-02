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
        Assert.Equal(playId, resultPlayId);
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

[SutProviderCustomize]
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

[SutProviderCustomize]
public class PlayIdSingletonServiceTests
{
    public static IEnumerable<object[]> SutProvider()
    {
        var sutProvider = new SutProvider<PlayIdSingletonService>();
        var httpContext = sutProvider.CreateDependency<HttpContext>();
        var serviceProvider = sutProvider.CreateDependency<IServiceProvider>();
        var hostEnvironment = sutProvider.CreateDependency<IHostEnvironment>();
        var playIdService = new PlayIdService(hostEnvironment);
        sutProvider.SetDependency(playIdService);
        httpContext.RequestServices.Returns(serviceProvider);
        serviceProvider.GetService<PlayIdService>().Returns(playIdService);
        serviceProvider.GetRequiredService<PlayIdService>().Returns(playIdService);
        sutProvider.CreateDependency<IHttpContextAccessor>().HttpContext.Returns(httpContext);
        sutProvider.Create();
        return [[sutProvider]];
    }

    private void PrepHttpContext(
        SutProvider<PlayIdSingletonService> sutProvider)
    {
        var httpContext = sutProvider.CreateDependency<HttpContext>();
        var serviceProvider = sutProvider.CreateDependency<IServiceProvider>();
        var PlayIdService = sutProvider.CreateDependency<PlayIdService>();
        httpContext.RequestServices.Returns(serviceProvider);
        serviceProvider.GetRequiredService<PlayIdService>().Returns(PlayIdService);
        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext.Returns(httpContext);
    }

    [Theory]
    [BitMemberAutoData(nameof(SutProvider))]
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
    [BitMemberAutoData(nameof(SutProvider))]
    public void InPlay_WhenNotDevelopment_ReturnsFalse(
        SutProvider<PlayIdSingletonService> sutProvider,
        string playIdValue)
    {
        var scopedPlayIdService = sutProvider.GetDependency<PlayIdService>();
        scopedPlayIdService.PlayId = playIdValue;
        sutProvider.GetDependency<IHostEnvironment>().EnvironmentName.Returns(Environments.Production);

        var result = sutProvider.Sut.InPlay(out var playId);

        Assert.False(result);
        Assert.Empty(playId);
    }

    [Theory]
    [BitMemberAutoData(nameof(SutProvider))]
    public void InPlay_WhenDevelopmentAndHttpContextWithPlayId_ReturnsTrue(
        SutProvider<PlayIdSingletonService> sutProvider,
        string playIdValue)
    {
        sutProvider.GetDependency<PlayIdService>().PlayId = playIdValue;
        sutProvider.GetDependency<IHostEnvironment>().EnvironmentName.Returns(Environments.Development);

        var result = sutProvider.Sut.InPlay(out var playId);

        Assert.True(result);
        Assert.Equal(playIdValue, playId);
    }

    [Theory]
    [BitMemberAutoData(nameof(SutProvider))]
    public void PlayId_SetterSetsOnScopedService(
        SutProvider<PlayIdSingletonService> sutProvider,
        string playIdValue)
    {
        var scopedPlayIdService = sutProvider.GetDependency<PlayIdService>();

        sutProvider.Sut.PlayId = playIdValue;

        Assert.Equal(playIdValue, scopedPlayIdService.PlayId);
    }

    [Theory]
    [BitMemberAutoData(nameof(SutProvider))]
    public void PlayId_WhenNoHttpContext_GetterReturnsNull(
        SutProvider<PlayIdSingletonService> sutProvider)
    {
        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext.Returns((HttpContext?)null);

        var result = sutProvider.Sut.PlayId;

        Assert.Null(result);
    }

    [Theory]
    [BitMemberAutoData(nameof(SutProvider))]
    public void PlayId_WhenNoHttpContext_SetterDoesNotThrow(
        SutProvider<PlayIdSingletonService> sutProvider,
        string playIdValue)
    {
        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext.Returns((HttpContext?)null);

        sutProvider.Sut.PlayId = playIdValue;
    }
}
