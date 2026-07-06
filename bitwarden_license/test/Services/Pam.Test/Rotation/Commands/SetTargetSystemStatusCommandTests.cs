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
public class SetTargetSystemStatusCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task SetStatusAsync_TargetMissing_ThrowsNotFound(Guid organizationId, Guid actingUserId, Guid targetSystemId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(targetSystemId).Returns((PamTargetSystem?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.SetStatusAsync(organizationId, actingUserId, targetSystemId, true));

        await sutProvider.GetDependency<IPamTargetSystemRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task SetStatusAsync_WrongOrg_ThrowsNotFound(Guid actingUserId, PamTargetSystem target)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.SetStatusAsync(Guid.NewGuid(), actingUserId, target.Id, true));

        await sutProvider.GetDependency<IPamTargetSystemRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task SetStatusAsync_AlreadyEnabled_ThrowsBadRequest(Guid actingUserId, PamTargetSystem target)
    {
        var sutProvider = Setup();
        target.Status = PamTargetSystemStatus.Active;
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SetStatusAsync(target.OrganizationId, actingUserId, target.Id, true));

        await sutProvider.GetDependency<IPamTargetSystemRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task SetStatusAsync_AlreadyDisabled_ThrowsBadRequest(Guid actingUserId, PamTargetSystem target)
    {
        var sutProvider = Setup();
        target.Status = PamTargetSystemStatus.Disabled;
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SetStatusAsync(target.OrganizationId, actingUserId, target.Id, false));

        await sutProvider.GetDependency<IPamTargetSystemRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task SetStatusAsync_Disable_UpdatesStatusAndEmitsDisabledAudit(Guid actingUserId, PamTargetSystem target)
    {
        var sutProvider = Setup();
        target.Status = PamTargetSystemStatus.Active;
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await sutProvider.Sut.SetStatusAsync(target.OrganizationId, actingUserId, target.Id, false);

        await sutProvider.GetDependency<IPamTargetSystemRepository>().Received(1).ReplaceAsync(Arg.Is<PamTargetSystem>(t =>
            t.Id == target.Id && t.Status == PamTargetSystemStatus.Disabled && t.RevisionDate == _now));
        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.TargetSystemDisabled && e.Phase == AccessAuditEventPhase.Attempt));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.TargetSystemDisabled && e.Phase == AccessAuditEventPhase.Outcome));
    }

    [Theory, BitAutoData]
    public async Task SetStatusAsync_Enable_UpdatesStatusAndEmitsEnabledAudit(Guid actingUserId, PamTargetSystem target)
    {
        var sutProvider = Setup();
        target.Status = PamTargetSystemStatus.Disabled;
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await sutProvider.Sut.SetStatusAsync(target.OrganizationId, actingUserId, target.Id, true);

        await sutProvider.GetDependency<IPamTargetSystemRepository>().Received(1).ReplaceAsync(Arg.Is<PamTargetSystem>(t =>
            t.Id == target.Id && t.Status == PamTargetSystemStatus.Active));
        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.TargetSystemEnabled && e.Phase == AccessAuditEventPhase.Attempt));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.TargetSystemEnabled && e.Phase == AccessAuditEventPhase.Outcome));
    }

    private static SutProvider<SetTargetSystemStatusCommand> Setup()
    {
        var sutProvider = new SutProvider<SetTargetSystemStatusCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
