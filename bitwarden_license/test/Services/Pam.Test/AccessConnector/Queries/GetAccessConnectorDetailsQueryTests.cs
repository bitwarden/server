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
/// ManageAccessConnectorRequirement only proves the caller administers the organization named in the route, so this
/// query's own OrganizationId check is the sole thing keeping an Owner of one organization from reading another's
/// access connector and the rotation activity it has worked.
/// </summary>
public class GetAccessConnectorDetailsQueryTests
{
    private static readonly DateTime _now = new(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task GetAsync_AccessConnectorMissing_ThrowsNotFound(Guid organizationId, Guid accessConnectorId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnectorId)
            .Returns((PamAccessConnector?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(organizationId, accessConnectorId));

        await sutProvider.GetDependency<IPamRotationJobRepository>().DidNotReceiveWithAnyArgs()
            .GetManyRecentByAccessConnectorIdAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_AccessConnectorBelongsToAnotherOrganization_ThrowsNotFound(
        Guid organizationId, PamAccessConnector accessConnector)
    {
        var sutProvider = Setup();
        accessConnector.OrganizationId = Guid.NewGuid();
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(organizationId, accessConnector.Id));

        await sutProvider.GetDependency<IPamRotationJobRepository>().DidNotReceiveWithAnyArgs()
            .GetManyRecentByAccessConnectorIdAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_AccessConnectorInTheRouteOrganization_ReturnsConnectionAssignmentsAndActivity(
        PamAccessConnector accessConnector, List<PamRotationJobDetails> jobs, Guid otherAccessConnectorId)
    {
        var sutProvider = Setup();
        accessConnector.LastHeartbeatAt =
            _now - new PamRotationOptions().AccessConnectorOfflineAfter + TimeSpan.FromSeconds(1);
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);
        var assigned = Assignment(accessConnector.OrganizationId, accessConnector.Id);
        sutProvider.GetDependency<IPamAccessConnectorRepository>()
            .GetAssignmentsByOrganizationIdAsync(accessConnector.OrganizationId)
            .Returns([assigned, Assignment(accessConnector.OrganizationId, otherAccessConnectorId)]);
        sutProvider.GetDependency<IPamRotationJobRepository>()
            .GetManyRecentByAccessConnectorIdAsync(accessConnector.Id, Arg.Any<int>())
            .Returns(jobs);

        var result = await sutProvider.Sut.GetAsync(accessConnector.OrganizationId, accessConnector.Id);

        Assert.Same(accessConnector, result.AccessConnector.AccessConnector);
        Assert.True(result.AccessConnector.IsConnected);
        // The fleet-wide assignment read is narrowed to this access connector's own targets.
        Assert.Equal([assigned.TargetSystemId], result.AccessConnector.AssignedTargetSystemIds);
        Assert.Equal(jobs.Count, result.Jobs.Count);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_HeartbeatOlderThanOfflineAfter_IsNotConnected(PamAccessConnector accessConnector)
    {
        var sutProvider = Setup();
        accessConnector.LastHeartbeatAt =
            _now - new PamRotationOptions().AccessConnectorOfflineAfter - TimeSpan.FromSeconds(1);
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);

        var result = await sutProvider.Sut.GetAsync(accessConnector.OrganizationId, accessConnector.Id);

        Assert.False(result.AccessConnector.IsConnected);
    }

    /// <summary>The activity section is capped rather than unbounded, so the read must carry a positive limit.</summary>
    [Theory, BitAutoData]
    public async Task GetAsync_ReadsABoundedNumberOfJobs(PamAccessConnector accessConnector)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);

        await sutProvider.Sut.GetAsync(accessConnector.OrganizationId, accessConnector.Id);

        await sutProvider.GetDependency<IPamRotationJobRepository>().Received(1)
            .GetManyRecentByAccessConnectorIdAsync(accessConnector.Id, Arg.Is<int>(limit => limit > 0));
    }

    private static SutProvider<GetAccessConnectorDetailsQuery> Setup()
    {
        var sutProvider = new SutProvider<GetAccessConnectorDetailsQuery>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        sutProvider.GetDependency<IOptions<PamRotationOptions>>().Value.Returns(new PamRotationOptions());
        return sutProvider;
    }

    private static PamAccessConnectorTargetAssignment Assignment(Guid organizationId, Guid accessConnectorId) => new()
    {
        Id = CombGuid.Generate(),
        AccessConnectorId = accessConnectorId,
        TargetSystemId = CombGuid.Generate(),
        OrganizationId = organizationId,
        CreationDate = _now,
    };
}
