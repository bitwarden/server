using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Rotation.Commands;

[SutProviderCustomize]
public class UnassignDaemonFromTargetCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task UnassignAsync_DaemonMissing_ThrowsNotFound(
        Guid organizationId, Guid actingUserId, Guid daemonId, Guid targetSystemId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemonId).Returns((PamDaemon?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UnassignAsync(organizationId, actingUserId, daemonId, targetSystemId));

        await sutProvider.GetDependency<IPamDaemonRepository>().DidNotReceiveWithAnyArgs()
            .DeleteAssignmentAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task UnassignAsync_DaemonWrongOrg_ThrowsNotFound(Guid actingUserId, PamDaemon daemon, Guid targetSystemId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UnassignAsync(Guid.NewGuid(), actingUserId, daemon.Id, targetSystemId));

        await sutProvider.GetDependency<IPamDaemonRepository>().DidNotReceiveWithAnyArgs()
            .DeleteAssignmentAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task UnassignAsync_TargetMissing_ThrowsNotFound(Guid actingUserId, PamDaemon daemon, Guid targetSystemId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(targetSystemId)
            .Returns((PamTargetSystem?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UnassignAsync(daemon.OrganizationId, actingUserId, daemon.Id, targetSystemId));

        await sutProvider.GetDependency<IPamDaemonRepository>().DidNotReceiveWithAnyArgs()
            .DeleteAssignmentAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task UnassignAsync_TargetWrongOrg_ThrowsNotFound(Guid actingUserId, PamDaemon daemon, PamTargetSystem target)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UnassignAsync(daemon.OrganizationId, actingUserId, daemon.Id, target.Id));

        await sutProvider.GetDependency<IPamDaemonRepository>().DidNotReceiveWithAnyArgs()
            .DeleteAssignmentAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task UnassignAsync_AssignmentMissing_ThrowsNotFound(Guid actingUserId, PamDaemon daemon, PamTargetSystem target)
    {
        var sutProvider = Setup();
        target.OrganizationId = daemon.OrganizationId;
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);
        sutProvider.GetDependency<IPamDaemonRepository>().AssignmentExistsAsync(daemon.Id, target.Id).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UnassignAsync(daemon.OrganizationId, actingUserId, daemon.Id, target.Id));

        await sutProvider.GetDependency<IPamDaemonRepository>().DidNotReceiveWithAnyArgs()
            .DeleteAssignmentAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task UnassignAsync_HappyPath_DeletesAssignment(Guid actingUserId, PamDaemon daemon, PamTargetSystem target)
    {
        var sutProvider = Setup();
        target.OrganizationId = daemon.OrganizationId;
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);
        sutProvider.GetDependency<IPamDaemonRepository>().AssignmentExistsAsync(daemon.Id, target.Id).Returns(true);

        await sutProvider.Sut.UnassignAsync(daemon.OrganizationId, actingUserId, daemon.Id, target.Id);

        await sutProvider.GetDependency<IPamDaemonRepository>().Received(1).DeleteAssignmentAsync(daemon.Id, target.Id);
    }

    [Theory, BitAutoData]
    public async Task UnassignAsync_HappyPath_EmitsAttemptThenOutcome(Guid actingUserId, PamDaemon daemon, PamTargetSystem target)
    {
        var sutProvider = Setup();
        target.OrganizationId = daemon.OrganizationId;
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);
        sutProvider.GetDependency<IPamDaemonRepository>().AssignmentExistsAsync(daemon.Id, target.Id).Returns(true);

        await sutProvider.Sut.UnassignAsync(daemon.OrganizationId, actingUserId, daemon.Id, target.Id);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.DaemonUnassignedFromTarget && e.Phase == AccessAuditEventPhase.Attempt
            && e.DaemonId == daemon.Id && e.TargetSystemId == target.Id));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.DaemonUnassignedFromTarget && e.Phase == AccessAuditEventPhase.Outcome
            && e.DaemonId == daemon.Id && e.TargetSystemId == target.Id));
    }

    private static SutProvider<UnassignDaemonFromTargetCommand> Setup()
    {
        var sutProvider = new SutProvider<UnassignDaemonFromTargetCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
