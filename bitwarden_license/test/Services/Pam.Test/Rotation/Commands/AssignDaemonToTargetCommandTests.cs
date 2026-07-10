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
public class AssignDaemonToTargetCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task AssignAsync_DaemonMissing_ThrowsNotFound(
        Guid organizationId, Guid actingUserId, Guid daemonId, Guid targetSystemId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemonId).Returns((PamDaemon?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.AssignAsync(organizationId, actingUserId, daemonId, targetSystemId));

        await sutProvider.GetDependency<IPamDaemonRepository>().DidNotReceiveWithAnyArgs()
            .CreateAssignmentAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task AssignAsync_DaemonWrongOrg_ThrowsNotFound(
        Guid actingUserId, PamDaemon daemon, Guid targetSystemId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);

        // daemon.OrganizationId is an unrelated AutoFixture Guid.
        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.AssignAsync(Guid.NewGuid(), actingUserId, daemon.Id, targetSystemId));

        await sutProvider.GetDependency<IPamDaemonRepository>().DidNotReceiveWithAnyArgs()
            .CreateAssignmentAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task AssignAsync_TargetMissing_ThrowsNotFound(Guid actingUserId, PamDaemon daemon, Guid targetSystemId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(targetSystemId)
            .Returns((PamTargetSystem?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.AssignAsync(daemon.OrganizationId, actingUserId, daemon.Id, targetSystemId));

        await sutProvider.GetDependency<IPamDaemonRepository>().DidNotReceiveWithAnyArgs()
            .CreateAssignmentAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task AssignAsync_TargetWrongOrg_ThrowsNotFound(Guid actingUserId, PamDaemon daemon, PamTargetSystem target)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        // Same org as the daemon, but not the caller's route org -- target.OrganizationId is unrelated to daemon.OrganizationId too.
        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.AssignAsync(daemon.OrganizationId, actingUserId, daemon.Id, target.Id));

        await sutProvider.GetDependency<IPamDaemonRepository>().DidNotReceiveWithAnyArgs()
            .CreateAssignmentAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task AssignAsync_DaemonDisabled_ThrowsBadRequest(Guid actingUserId, PamDaemon daemon, PamTargetSystem target)
    {
        var sutProvider = Setup();
        daemon.Status = PamDaemonStatus.Disabled;
        target.OrganizationId = daemon.OrganizationId;
        target.Method = PamTargetSystemMethod.Automatic;
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AssignAsync(daemon.OrganizationId, actingUserId, daemon.Id, target.Id));

        await sutProvider.GetDependency<IPamDaemonRepository>().DidNotReceiveWithAnyArgs()
            .CreateAssignmentAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task AssignAsync_TargetManual_ThrowsBadRequest(Guid actingUserId, PamDaemon daemon, PamTargetSystem target)
    {
        var sutProvider = Setup();
        daemon.Status = PamDaemonStatus.Enabled;
        target.OrganizationId = daemon.OrganizationId;
        target.Method = PamTargetSystemMethod.Manual;
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AssignAsync(daemon.OrganizationId, actingUserId, daemon.Id, target.Id));

        await sutProvider.GetDependency<IPamDaemonRepository>().DidNotReceiveWithAnyArgs()
            .CreateAssignmentAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task AssignAsync_DuplicateAssignment_ThrowsBadRequest(Guid actingUserId, PamDaemon daemon, PamTargetSystem target)
    {
        var sutProvider = Setup();
        daemon.Status = PamDaemonStatus.Enabled;
        target.OrganizationId = daemon.OrganizationId;
        target.Method = PamTargetSystemMethod.Automatic;
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);
        sutProvider.GetDependency<IPamDaemonRepository>().AssignmentExistsAsync(daemon.Id, target.Id).Returns(true);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AssignAsync(daemon.OrganizationId, actingUserId, daemon.Id, target.Id));

        await sutProvider.GetDependency<IPamDaemonRepository>().DidNotReceiveWithAnyArgs()
            .CreateAssignmentAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task AssignAsync_HappyPath_CreatesAssignment(Guid actingUserId, PamDaemon daemon, PamTargetSystem target)
    {
        var sutProvider = Setup();
        daemon.Status = PamDaemonStatus.Enabled;
        target.OrganizationId = daemon.OrganizationId;
        target.Method = PamTargetSystemMethod.Automatic;
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);
        sutProvider.GetDependency<IPamDaemonRepository>().AssignmentExistsAsync(daemon.Id, target.Id).Returns(false);

        await sutProvider.Sut.AssignAsync(daemon.OrganizationId, actingUserId, daemon.Id, target.Id);

        await sutProvider.GetDependency<IPamDaemonRepository>().Received(1).CreateAssignmentAsync(Arg.Is<PamDaemonTargetAssignment>(a =>
            a.DaemonId == daemon.Id && a.TargetSystemId == target.Id && a.OrganizationId == daemon.OrganizationId
            && a.Id != Guid.Empty && a.CreationDate == _now));
    }

    [Theory, BitAutoData]
    public async Task AssignAsync_HappyPath_EmitsAttemptThenOutcome(Guid actingUserId, PamDaemon daemon, PamTargetSystem target)
    {
        var sutProvider = Setup();
        daemon.Status = PamDaemonStatus.Enabled;
        target.OrganizationId = daemon.OrganizationId;
        target.Method = PamTargetSystemMethod.Automatic;
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);
        sutProvider.GetDependency<IPamDaemonRepository>().AssignmentExistsAsync(daemon.Id, target.Id).Returns(false);

        await sutProvider.Sut.AssignAsync(daemon.OrganizationId, actingUserId, daemon.Id, target.Id);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.DaemonAssignedToTarget && e.Phase == AccessAuditEventPhase.Attempt
            && e.DaemonId == daemon.Id && e.TargetSystemId == target.Id));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.DaemonAssignedToTarget && e.Phase == AccessAuditEventPhase.Outcome
            && e.DaemonId == daemon.Id && e.TargetSystemId == target.Id));
    }

    private static SutProvider<AssignDaemonToTargetCommand> Setup()
    {
        var sutProvider = new SutProvider<AssignDaemonToTargetCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
