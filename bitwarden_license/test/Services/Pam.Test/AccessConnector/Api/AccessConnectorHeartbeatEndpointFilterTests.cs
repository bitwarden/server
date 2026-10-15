using System.Runtime.CompilerServices;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector;
using Bit.Services.Pam.AccessConnector.Api.Endpoints.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.AccessConnector.Api;

/// <remarks>
/// The filter does not check connector eligibility; PamAccessConnectorClientProviderTests and
/// PamRotationJobRepositoryTests cover it.
/// </remarks>
public class AccessConnectorHeartbeatEndpointFilterTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan _heartbeatMinInterval = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task InvokeAsync_NoPamAccessConnectorIdInContext_ThrowsNotFound_SkipsNext()
    {
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.PamAccessConnectorId.Returns((Guid?)null);
        var accessConnectorRepository = Substitute.For<IPamAccessConnectorRepository>();
        var (context, nextCalled) = CreateContext(currentContext, accessConnectorRepository);

        await Assert.ThrowsAsync<NotFoundException>(
            () => new AccessConnectorHeartbeatEndpointFilter().InvokeAsync(context, NextDelegate(nextCalled)).AsTask());

        Assert.False(nextCalled.Value);
        await accessConnectorRepository.DidNotReceiveWithAnyArgs().UpdateHeartbeatAsync(default, default, default);
    }

    [Fact]
    public async Task InvokeAsync_AccessConnectorIdInContext_BumpsHeartbeatAndCallsNext()
    {
        var accessConnectorId = Guid.NewGuid();
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.PamAccessConnectorId.Returns(accessConnectorId);
        var accessConnectorRepository = Substitute.For<IPamAccessConnectorRepository>();
        var (context, nextCalled) = CreateContext(currentContext, accessConnectorRepository);

        var result = await new AccessConnectorHeartbeatEndpointFilter().InvokeAsync(context, NextDelegate(nextCalled));

        Assert.True(nextCalled.Value);
        Assert.Equal("ok", result);
        await accessConnectorRepository
            .Received(1).UpdateHeartbeatAsync(accessConnectorId, _now, _heartbeatMinInterval);
    }

    /// <remarks>
    /// The id comes off the token, so the poll route a connector hits continuously pays one conditional write and no
    /// reads.
    /// </remarks>
    [Fact]
    public async Task InvokeAsync_AccessConnectorIdInContext_WritesTheHeartbeatWithoutReadingTheAccessConnectorRow()
    {
        var accessConnectorId = Guid.NewGuid();
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.PamAccessConnectorId.Returns(accessConnectorId);
        var accessConnectorRepository = Substitute.For<IPamAccessConnectorRepository>();
        var (context, nextCalled) = CreateContext(currentContext, accessConnectorRepository);

        await new AccessConnectorHeartbeatEndpointFilter().InvokeAsync(context, NextDelegate(nextCalled));

        await accessConnectorRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default);
    }

    private static EndpointFilterDelegate NextDelegate(StrongBox<bool> nextCalled) => _ =>
    {
        nextCalled.Value = true;
        return ValueTask.FromResult<object?>("ok");
    };

    private static (EndpointFilterInvocationContext Context, StrongBox<bool> NextCalled) CreateContext(
        ICurrentContext currentContext, IPamAccessConnectorRepository accessConnectorRepository)
    {
        var timeProvider = new FakeTimeProvider();
        timeProvider.SetUtcNow(_now);
        var services = new ServiceCollection();
        services.AddSingleton(currentContext);
        services.AddSingleton(accessConnectorRepository);
        services.AddSingleton<IOptions<PamRotationOptions>>(
            Options.Create(new PamRotationOptions { HeartbeatMinInterval = _heartbeatMinInterval }));
        services.AddSingleton<TimeProvider>(timeProvider);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        return (EndpointFilterInvocationContext.Create(httpContext), new StrongBox<bool>(false));
    }
}
