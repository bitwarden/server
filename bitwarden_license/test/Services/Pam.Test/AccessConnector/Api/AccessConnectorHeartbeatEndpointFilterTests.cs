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
/// The filter's whole job is the heartbeat write, so these cover the write and its one guard. Daemon eligibility
/// is not tested here because the filter does not check it: token issuance and the job queries do, and they have
/// their own coverage (PamDaemonClientProviderTests, PamRotationJobRepositoryTests).
/// </remarks>
public class AccessConnectorHeartbeatEndpointFilterTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan _heartbeatMinInterval = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task InvokeAsync_NoPamDaemonIdInContext_ThrowsNotFound_SkipsNext()
    {
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.PamDaemonId.Returns((Guid?)null);
        var daemonRepository = Substitute.For<IPamDaemonRepository>();
        var (context, nextCalled) = CreateContext(currentContext, daemonRepository);

        await Assert.ThrowsAsync<NotFoundException>(
            () => new AccessConnectorHeartbeatEndpointFilter().InvokeAsync(context, NextDelegate(nextCalled)).AsTask());

        Assert.False(nextCalled.Value);
        await daemonRepository.DidNotReceiveWithAnyArgs().UpdateHeartbeatAsync(default, default, default);
    }

    [Fact]
    public async Task InvokeAsync_DaemonIdInContext_BumpsHeartbeatAndCallsNext()
    {
        var daemonId = Guid.NewGuid();
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.PamDaemonId.Returns(daemonId);
        var daemonRepository = Substitute.For<IPamDaemonRepository>();
        var (context, nextCalled) = CreateContext(currentContext, daemonRepository);

        var result = await new AccessConnectorHeartbeatEndpointFilter().InvokeAsync(context, NextDelegate(nextCalled));

        Assert.True(nextCalled.Value);
        Assert.Equal("ok", result);
        await daemonRepository.Received(1).UpdateHeartbeatAsync(daemonId, _now, _heartbeatMinInterval);
    }

    /// <remarks>
    /// The daemon id comes straight off the token, so the poll route -- the one a daemon hits continuously -- pays
    /// one conditional write and no reads.
    /// </remarks>
    [Fact]
    public async Task InvokeAsync_DaemonIdInContext_WritesTheHeartbeatWithoutReadingTheDaemonRow()
    {
        var daemonId = Guid.NewGuid();
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.PamDaemonId.Returns(daemonId);
        var daemonRepository = Substitute.For<IPamDaemonRepository>();
        var (context, nextCalled) = CreateContext(currentContext, daemonRepository);

        await new AccessConnectorHeartbeatEndpointFilter().InvokeAsync(context, NextDelegate(nextCalled));

        await daemonRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default);
    }

    private static EndpointFilterDelegate NextDelegate(StrongBox<bool> nextCalled) => _ =>
    {
        nextCalled.Value = true;
        return ValueTask.FromResult<object?>("ok");
    };

    private static (EndpointFilterInvocationContext Context, StrongBox<bool> NextCalled) CreateContext(
        ICurrentContext currentContext, IPamDaemonRepository daemonRepository)
    {
        var timeProvider = new FakeTimeProvider();
        timeProvider.SetUtcNow(_now);
        var services = new ServiceCollection();
        services.AddSingleton(currentContext);
        services.AddSingleton(daemonRepository);
        services.AddSingleton<IOptions<PamRotationOptions>>(
            Options.Create(new PamRotationOptions { HeartbeatMinInterval = _heartbeatMinInterval }));
        services.AddSingleton<TimeProvider>(timeProvider);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        return (EndpointFilterInvocationContext.Create(httpContext), new StrongBox<bool>(false));
    }
}
