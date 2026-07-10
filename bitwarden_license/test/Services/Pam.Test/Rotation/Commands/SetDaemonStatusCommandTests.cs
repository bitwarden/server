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
public class SetDaemonStatusCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task SetStatusAsync_DaemonMissing_ThrowsNotFound(Guid organizationId, Guid actingUserId, Guid daemonId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemonId).Returns((PamDaemon?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.SetStatusAsync(organizationId, actingUserId, daemonId, enable: false));

        await sutProvider.GetDependency<IPamDaemonRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task SetStatusAsync_WrongOrg_ThrowsNotFound(Guid actingUserId, PamDaemon daemon)
    {
        var sutProvider = Setup();
        daemon.Status = PamDaemonStatus.Enabled;
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);

        // daemon.OrganizationId is an unrelated AutoFixture Guid -- a cross-org lookup must 404, never leak existence.
        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.SetStatusAsync(Guid.NewGuid(), actingUserId, daemon.Id, enable: false));

        await sutProvider.GetDependency<IPamDaemonRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task SetStatusAsync_Disable_AlreadyDisabled_ThrowsBadRequest(Guid actingUserId, PamDaemon daemon)
    {
        var sutProvider = Setup();
        daemon.Status = PamDaemonStatus.Disabled;
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SetStatusAsync(daemon.OrganizationId, actingUserId, daemon.Id, enable: false));

        await sutProvider.GetDependency<IPamDaemonRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task SetStatusAsync_Enable_AlreadyEnabled_ThrowsBadRequest(Guid actingUserId, PamDaemon daemon)
    {
        var sutProvider = Setup();
        daemon.Status = PamDaemonStatus.Enabled;
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SetStatusAsync(daemon.OrganizationId, actingUserId, daemon.Id, enable: true));

        await sutProvider.GetDependency<IPamDaemonRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task SetStatusAsync_Disable_SetsDisabled(Guid actingUserId, PamDaemon daemon)
    {
        var sutProvider = Setup();
        daemon.Status = PamDaemonStatus.Enabled;
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);

        await sutProvider.Sut.SetStatusAsync(daemon.OrganizationId, actingUserId, daemon.Id, enable: false);

        await sutProvider.GetDependency<IPamDaemonRepository>().Received(1).ReplaceAsync(Arg.Is<PamDaemon>(d =>
            d.Id == daemon.Id && d.Status == PamDaemonStatus.Disabled && d.RevisionDate == _now));
    }

    [Theory, BitAutoData]
    public async Task SetStatusAsync_Enable_SetsEnabled(Guid actingUserId, PamDaemon daemon)
    {
        var sutProvider = Setup();
        daemon.Status = PamDaemonStatus.Disabled;
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);

        await sutProvider.Sut.SetStatusAsync(daemon.OrganizationId, actingUserId, daemon.Id, enable: true);

        await sutProvider.GetDependency<IPamDaemonRepository>().Received(1).ReplaceAsync(Arg.Is<PamDaemon>(d =>
            d.Id == daemon.Id && d.Status == PamDaemonStatus.Enabled && d.RevisionDate == _now));
    }

    [Theory, BitAutoData]
    public async Task SetStatusAsync_Disable_EmitsAttemptThenOutcome(Guid actingUserId, PamDaemon daemon)
    {
        var sutProvider = Setup();
        daemon.Status = PamDaemonStatus.Enabled;
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);

        await sutProvider.Sut.SetStatusAsync(daemon.OrganizationId, actingUserId, daemon.Id, enable: false);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.DaemonDisabled && e.Phase == AccessAuditEventPhase.Attempt
            && e.DaemonId == daemon.Id && e.DaemonName == daemon.Name && e.ActorId == actingUserId));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.DaemonDisabled && e.Phase == AccessAuditEventPhase.Outcome
            && e.DaemonId == daemon.Id));
    }

    [Theory, BitAutoData]
    public async Task SetStatusAsync_Enable_EmitsEnabledKind(Guid actingUserId, PamDaemon daemon)
    {
        var sutProvider = Setup();
        daemon.Status = PamDaemonStatus.Disabled;
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);

        await sutProvider.Sut.SetStatusAsync(daemon.OrganizationId, actingUserId, daemon.Id, enable: true);

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(1).EmitAsync(
            Arg.Is<AccessAuditEventData>(e =>
                e.Kind == AccessAuditEventKind.DaemonEnabled && e.Phase == AccessAuditEventPhase.Outcome
                && e.DaemonId == daemon.Id));
    }

    private static SutProvider<SetDaemonStatusCommand> Setup()
    {
        var sutProvider = new SutProvider<SetDaemonStatusCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
