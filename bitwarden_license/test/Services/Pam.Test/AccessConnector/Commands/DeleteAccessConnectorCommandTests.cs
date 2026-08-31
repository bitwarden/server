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
public class DeleteAccessConnectorCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task DeleteAsync_DaemonMissing_ThrowsNotFound(Guid organizationId, Guid actingUserId, Guid daemonId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemonId).Returns((PamDaemon?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.DeleteAsync(organizationId, actingUserId, daemonId));

        await sutProvider.GetDependency<IPamDaemonRepository>().DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_WrongOrg_ThrowsNotFound(Guid actingUserId, PamDaemon daemon)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);

        // daemon.OrganizationId is an unrelated AutoFixture Guid -- a cross-org lookup must 404, never leak existence.
        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.DeleteAsync(Guid.NewGuid(), actingUserId, daemon.Id));

        await sutProvider.GetDependency<IPamDaemonRepository>().DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_DeletesTheDaemonThroughTheRepositoryCascade(Guid actingUserId, PamDaemon daemon)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);

        await sutProvider.Sut.DeleteAsync(daemon.OrganizationId, actingUserId, daemon.Id);

        await sutProvider.GetDependency<IPamDaemonRepository>().Received(1).DeleteAsync(daemon);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_EmitsAttemptThenOutcome(Guid actingUserId, PamDaemon daemon)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);

        await sutProvider.Sut.DeleteAsync(daemon.OrganizationId, actingUserId, daemon.Id);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.DaemonDeleted && e.Phase == AccessAuditEventPhase.Attempt
            && e.DaemonId == daemon.Id && e.DaemonName == daemon.Name && e.ActorId == actingUserId));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.DaemonDeleted && e.Phase == AccessAuditEventPhase.Outcome
            && e.DaemonId == daemon.Id));
    }

    private static SutProvider<DeleteAccessConnectorCommand> Setup()
    {
        var sutProvider = new SutProvider<DeleteAccessConnectorCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
