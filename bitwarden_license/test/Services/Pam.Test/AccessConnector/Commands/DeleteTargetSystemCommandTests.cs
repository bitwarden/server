using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.AccessConnector.Commands;

[SutProviderCustomize]
public class DeleteTargetSystemCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task DeleteAsync_TargetMissing_ThrowsNotFound(
        Guid organizationId, Guid actingUserId, Guid targetSystemId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(targetSystemId)
            .Returns((PamTargetSystem?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.DeleteAsync(organizationId, actingUserId, targetSystemId));

        await sutProvider.GetDependency<IPamTargetSystemRepository>().DidNotReceiveWithAnyArgs()
            .DeleteWithAssignmentsAsync(default);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_WrongOrg_ThrowsNotFound(Guid actingUserId, PamTargetSystem target)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.DeleteAsync(Guid.NewGuid(), actingUserId, target.Id));

        await sutProvider.GetDependency<IPamTargetSystemRepository>().DidNotReceiveWithAnyArgs()
            .DeleteWithAssignmentsAsync(default);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_RotationConfigOnTarget_ThrowsBadRequestAndAuditsNothing(
        Guid actingUserId, PamTargetSystem target)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);
        sutProvider.GetDependency<IPamRotationConfigRepository>().AnyByTargetSystemAsync(target.Id).Returns(true);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.DeleteAsync(target.OrganizationId, actingUserId, target.Id));

        await sutProvider.GetDependency<IPamTargetSystemRepository>().DidNotReceiveWithAnyArgs()
            .DeleteWithAssignmentsAsync(default);
        // The common refusal is caught ahead of the audit, so it leaves no in-doubt Attempt in the trail.
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs()
            .EmitAsync(default!);
    }

    [Theory]
    [BitAutoData(PamTargetSystemStatus.Active)]
    [BitAutoData(PamTargetSystemStatus.Disabled)]
    public async Task DeleteAsync_AnyStatus_CallsDeleteWithAssignmentsAsync(
        PamTargetSystemStatus status, Guid actingUserId, PamTargetSystem target)
    {
        var sutProvider = Setup();
        target.Status = status;
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await sutProvider.Sut.DeleteAsync(target.OrganizationId, actingUserId, target.Id);

        await sutProvider.GetDependency<IPamTargetSystemRepository>().Received(1)
            .DeleteWithAssignmentsAsync(target.Id);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_ConfigCreatedAfterTheGuardRead_ThrowsBadRequest(
        Guid actingUserId, PamTargetSystem target)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);
        // The repository's own guard re-checks under lock and refuses once a config has been created in the window.
        sutProvider.GetDependency<IPamTargetSystemRepository>().DeleteWithAssignmentsAsync(target.Id).Returns(false);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.DeleteAsync(target.OrganizationId, actingUserId, target.Id));

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceive()
            .EmitAsync(Arg.Is<AccessAuditEventData>(e => e.Phase == AccessAuditEventPhase.Outcome));
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_HappyPath_EmitsAttemptThenOutcomeWithPreCapturedName(
        Guid actingUserId, PamTargetSystem target)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await sutProvider.Sut.DeleteAsync(target.OrganizationId, actingUserId, target.Id);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.TargetSystemDeleted && e.Phase == AccessAuditEventPhase.Attempt
            && e.OrganizationId == target.OrganizationId && e.ActorId == actingUserId
            && e.TargetSystemId == target.Id && e.TargetSystemName == target.Name
            && e.OccurredAt == _now));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.TargetSystemDeleted && e.Phase == AccessAuditEventPhase.Outcome
            && e.TargetSystemId == target.Id && e.TargetSystemName == target.Name));
    }

    private static SutProvider<DeleteTargetSystemCommand> Setup()
    {
        var sutProvider = new SutProvider<DeleteTargetSystemCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        sutProvider.GetDependency<IPamRotationConfigRepository>().AnyByTargetSystemAsync(Arg.Any<Guid>())
            .Returns(false);
        sutProvider.GetDependency<IPamTargetSystemRepository>().DeleteWithAssignmentsAsync(Arg.Any<Guid>())
            .Returns(true);
        return sutProvider;
    }
}
