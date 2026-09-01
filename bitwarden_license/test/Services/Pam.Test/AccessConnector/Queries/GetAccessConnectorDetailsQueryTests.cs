using Bit.Core.Exceptions;
using Bit.Core.Utilities;
using Bit.Pam.Entities;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector;
using Bit.Services.Pam.AccessConnector.Queries;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.AccessConnector.Queries;

/// <summary>
/// ManageAccessConnectorRequirement only proves the caller administers the organization named in the route, so this query's
/// own OrganizationId check is the sole thing keeping an Owner of one organization from reading another's daemon and
/// the rotation activity it has worked.
/// </summary>
public class GetAccessConnectorDetailsQueryTests
{
    private static readonly DateTime _now = new(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task GetAsync_DaemonMissing_ThrowsNotFound(Guid organizationId, Guid daemonId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemonId).Returns((PamDaemon?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(organizationId, daemonId));

        await sutProvider.GetDependency<IPamRotationJobRepository>().DidNotReceiveWithAnyArgs()
            .GetManyRecentByDaemonIdAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_DaemonBelongsToAnotherOrganization_ThrowsNotFound(Guid organizationId, PamDaemon daemon)
    {
        var sutProvider = Setup();
        daemon.OrganizationId = Guid.NewGuid();
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(organizationId, daemon.Id));

        await sutProvider.GetDependency<IPamRotationJobRepository>().DidNotReceiveWithAnyArgs()
            .GetManyRecentByDaemonIdAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_DaemonInTheRouteOrganization_ReturnsConnectionAssignmentsAndActivity(
        PamDaemon daemon, List<PamRotationJobDetails> jobs, Guid otherDaemonId)
    {
        var sutProvider = Setup();
        daemon.LastHeartbeatAt = _now - new PamRotationOptions().DaemonOfflineAfter + TimeSpan.FromSeconds(1);
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);
        var assigned = Assignment(daemon.OrganizationId, daemon.Id);
        sutProvider.GetDependency<IPamDaemonRepository>()
            .GetAssignmentsByOrganizationIdAsync(daemon.OrganizationId)
            .Returns([assigned, Assignment(daemon.OrganizationId, otherDaemonId)]);
        sutProvider.GetDependency<IPamRotationJobRepository>()
            .GetManyRecentByDaemonIdAsync(daemon.Id, Arg.Any<int>())
            .Returns(jobs);

        var result = await sutProvider.Sut.GetAsync(daemon.OrganizationId, daemon.Id);

        Assert.Same(daemon, result.Daemon.Daemon);
        Assert.True(result.Daemon.IsConnected);
        // The fleet-wide assignment read is narrowed to this daemon's own targets.
        Assert.Equal([assigned.TargetSystemId], result.Daemon.AssignedTargetSystemIds);
        Assert.Equal(jobs.Count, result.Jobs.Count);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_HeartbeatOlderThanOfflineAfter_IsNotConnected(PamDaemon daemon)
    {
        var sutProvider = Setup();
        daemon.LastHeartbeatAt = _now - new PamRotationOptions().DaemonOfflineAfter - TimeSpan.FromSeconds(1);
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);

        var result = await sutProvider.Sut.GetAsync(daemon.OrganizationId, daemon.Id);

        Assert.False(result.Daemon.IsConnected);
    }

    /// <summary>The activity section is capped rather than unbounded, so the read must carry a positive limit.</summary>
    [Theory, BitAutoData]
    public async Task GetAsync_ReadsABoundedNumberOfJobs(PamDaemon daemon)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);

        await sutProvider.Sut.GetAsync(daemon.OrganizationId, daemon.Id);

        await sutProvider.GetDependency<IPamRotationJobRepository>().Received(1)
            .GetManyRecentByDaemonIdAsync(daemon.Id, Arg.Is<int>(limit => limit > 0));
    }

    private static SutProvider<GetAccessConnectorDetailsQuery> Setup()
    {
        var sutProvider = new SutProvider<GetAccessConnectorDetailsQuery>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        sutProvider.GetDependency<IOptions<PamRotationOptions>>().Value.Returns(new PamRotationOptions());
        return sutProvider;
    }

    private static PamDaemonTargetAssignment Assignment(Guid organizationId, Guid daemonId) => new()
    {
        Id = CombGuid.Generate(),
        DaemonId = daemonId,
        TargetSystemId = CombGuid.Generate(),
        OrganizationId = organizationId,
        CreationDate = _now,
    };
}
