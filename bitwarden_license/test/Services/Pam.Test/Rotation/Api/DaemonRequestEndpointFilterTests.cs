using System.Runtime.CompilerServices;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Rotation;
using Bit.Services.Pam.Rotation.Api.Endpoints.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Rotation.Api;

public class DaemonRequestEndpointFilterTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan _heartbeatMinInterval = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task InvokeAsync_NoPamDaemonIdInContext_ThrowsNotFound_SkipsNext()
    {
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.PamDaemonId.Returns((Guid?)null);
        var daemonRepository = Substitute.For<IPamDaemonRepository>();
        var organizationAbilityCacheService = Substitute.For<IOrganizationAbilityCacheService>();
        var (context, nextCalled) = CreateContext(currentContext, daemonRepository, organizationAbilityCacheService);

        await Assert.ThrowsAsync<NotFoundException>(
            () => new DaemonRequestEndpointFilter().InvokeAsync(context, NextDelegate(nextCalled)).AsTask());

        Assert.False(nextCalled.Value);
        await daemonRepository.DidNotReceiveWithAnyArgs().UpdateHeartbeatAsync(default, default, default);
    }

    [Fact]
    public async Task InvokeAsync_DaemonMissing_ThrowsNotFound_SkipsNext()
    {
        var daemonId = Guid.NewGuid();
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.PamDaemonId.Returns(daemonId);
        var daemonRepository = Substitute.For<IPamDaemonRepository>();
        daemonRepository.GetByIdAsync(daemonId).Returns((PamDaemon?)null);
        var organizationAbilityCacheService = Substitute.For<IOrganizationAbilityCacheService>();
        var (context, nextCalled) = CreateContext(currentContext, daemonRepository, organizationAbilityCacheService);

        await Assert.ThrowsAsync<NotFoundException>(
            () => new DaemonRequestEndpointFilter().InvokeAsync(context, NextDelegate(nextCalled)).AsTask());

        Assert.False(nextCalled.Value);
        await daemonRepository.DidNotReceiveWithAnyArgs().UpdateHeartbeatAsync(default, default, default);
    }

    [Fact]
    public async Task InvokeAsync_DaemonRevoked_ThrowsNotFound_SkipsNext()
    {
        var daemonId = Guid.NewGuid();
        var daemon = new PamDaemon { Id = daemonId, OrganizationId = Guid.NewGuid(), Status = PamDaemonStatus.Revoked };
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.PamDaemonId.Returns(daemonId);
        var daemonRepository = Substitute.For<IPamDaemonRepository>();
        daemonRepository.GetByIdAsync(daemonId).Returns(daemon);
        var organizationAbilityCacheService = Substitute.For<IOrganizationAbilityCacheService>();
        var (context, nextCalled) = CreateContext(currentContext, daemonRepository, organizationAbilityCacheService);

        await Assert.ThrowsAsync<NotFoundException>(
            () => new DaemonRequestEndpointFilter().InvokeAsync(context, NextDelegate(nextCalled)).AsTask());

        Assert.False(nextCalled.Value);
        await daemonRepository.DidNotReceiveWithAnyArgs().UpdateHeartbeatAsync(default, default, default);
    }

    [Fact]
    public async Task InvokeAsync_OrganizationDisabled_ThrowsNotFound_SkipsNext()
    {
        var daemonId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var daemon = new PamDaemon { Id = daemonId, OrganizationId = orgId, Status = PamDaemonStatus.Enrolled };
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.PamDaemonId.Returns(daemonId);
        var daemonRepository = Substitute.For<IPamDaemonRepository>();
        daemonRepository.GetByIdAsync(daemonId).Returns(daemon);
        var organizationAbilityCacheService = Substitute.For<IOrganizationAbilityCacheService>();
        organizationAbilityCacheService.GetOrganizationAbilityAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(new OrganizationAbility { Id = orgId, Enabled = false, UsePam = true });
        var (context, nextCalled) = CreateContext(currentContext, daemonRepository, organizationAbilityCacheService);

        await Assert.ThrowsAsync<NotFoundException>(
            () => new DaemonRequestEndpointFilter().InvokeAsync(context, NextDelegate(nextCalled)).AsTask());

        Assert.False(nextCalled.Value);
        await daemonRepository.DidNotReceiveWithAnyArgs().UpdateHeartbeatAsync(default, default, default);
    }

    [Fact]
    public async Task InvokeAsync_OrganizationUsePamFalse_ThrowsNotFound_SkipsNext()
    {
        var daemonId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var daemon = new PamDaemon { Id = daemonId, OrganizationId = orgId, Status = PamDaemonStatus.Enrolled };
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.PamDaemonId.Returns(daemonId);
        var daemonRepository = Substitute.For<IPamDaemonRepository>();
        daemonRepository.GetByIdAsync(daemonId).Returns(daemon);
        var organizationAbilityCacheService = Substitute.For<IOrganizationAbilityCacheService>();
        organizationAbilityCacheService.GetOrganizationAbilityAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(new OrganizationAbility { Id = orgId, Enabled = true, UsePam = false });
        var (context, nextCalled) = CreateContext(currentContext, daemonRepository, organizationAbilityCacheService);

        await Assert.ThrowsAsync<NotFoundException>(
            () => new DaemonRequestEndpointFilter().InvokeAsync(context, NextDelegate(nextCalled)).AsTask());

        Assert.False(nextCalled.Value);
        await daemonRepository.DidNotReceiveWithAnyArgs().UpdateHeartbeatAsync(default, default, default);
    }

    [Fact]
    public async Task InvokeAsync_HappyPath_CallsNextAndUpdatesHeartbeatWithMinInterval()
    {
        var daemonId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var daemon = new PamDaemon { Id = daemonId, OrganizationId = orgId, Status = PamDaemonStatus.Enrolled };
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.PamDaemonId.Returns(daemonId);
        var daemonRepository = Substitute.For<IPamDaemonRepository>();
        daemonRepository.GetByIdAsync(daemonId).Returns(daemon);
        var organizationAbilityCacheService = Substitute.For<IOrganizationAbilityCacheService>();
        organizationAbilityCacheService.GetOrganizationAbilityAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(new OrganizationAbility { Id = orgId, Enabled = true, UsePam = true });
        var (context, nextCalled) = CreateContext(currentContext, daemonRepository, organizationAbilityCacheService);

        var result = await new DaemonRequestEndpointFilter().InvokeAsync(context, NextDelegate(nextCalled));

        Assert.True(nextCalled.Value);
        Assert.Equal("ok", result);
        await daemonRepository.Received(1).UpdateHeartbeatAsync(daemonId, _now, _heartbeatMinInterval);
        Assert.Same(daemon, context.HttpContext.Items[DaemonRequestEndpointFilter.PamDaemonHttpContextKey]);
    }

    private static EndpointFilterDelegate NextDelegate(StrongBox<bool> nextCalled) => _ =>
    {
        nextCalled.Value = true;
        return ValueTask.FromResult<object?>("ok");
    };

    private static (EndpointFilterInvocationContext Context, StrongBox<bool> NextCalled) CreateContext(
        ICurrentContext currentContext, IPamDaemonRepository daemonRepository,
        IOrganizationAbilityCacheService organizationAbilityCacheService)
    {
        var timeProvider = new FakeTimeProvider();
        timeProvider.SetUtcNow(_now);
        var services = new ServiceCollection();
        services.AddSingleton(currentContext);
        services.AddSingleton(daemonRepository);
        services.AddSingleton(organizationAbilityCacheService);
        services.AddSingleton<IOptions<PamRotationOptions>>(
            Options.Create(new PamRotationOptions { HeartbeatMinInterval = _heartbeatMinInterval }));
        services.AddSingleton<TimeProvider>(timeProvider);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        return (EndpointFilterInvocationContext.Create(httpContext), new StrongBox<bool>(false));
    }
}
