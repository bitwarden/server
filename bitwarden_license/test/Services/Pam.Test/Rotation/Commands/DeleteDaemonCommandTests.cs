using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
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
public class DeleteDaemonCommandTests
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
        await sutProvider.GetDependency<IApiKeyRepository>().DidNotReceiveWithAnyArgs().DeleteAsync(default!);
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
        await sutProvider.GetDependency<IApiKeyRepository>().DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_DeletesDaemonThenApiKey(Guid actingUserId, PamDaemon daemon, ApiKey apiKey)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);
        sutProvider.GetDependency<IApiKeyRepository>().GetByIdAsync(daemon.ApiKeyId).Returns(apiKey);

        await sutProvider.Sut.DeleteAsync(daemon.OrganizationId, actingUserId, daemon.Id);

        // The daemon row must be removed before its credential -- the PamDaemon -> ApiKey FK is ON DELETE NO ACTION.
        Received.InOrder(() =>
        {
            sutProvider.GetDependency<IPamDaemonRepository>().DeleteAsync(daemon);
            sutProvider.GetDependency<IApiKeyRepository>().DeleteAsync(apiKey);
        });
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_ApiKeyAlreadyGone_StillDeletesDaemonWithoutThrowing(Guid actingUserId, PamDaemon daemon)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);
        sutProvider.GetDependency<IApiKeyRepository>().GetByIdAsync(daemon.ApiKeyId).Returns((ApiKey?)null);

        await sutProvider.Sut.DeleteAsync(daemon.OrganizationId, actingUserId, daemon.Id);

        await sutProvider.GetDependency<IPamDaemonRepository>().Received(1).DeleteAsync(daemon);
        await sutProvider.GetDependency<IApiKeyRepository>().DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_EmitsAttemptThenOutcome(Guid actingUserId, PamDaemon daemon, ApiKey apiKey)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);
        sutProvider.GetDependency<IApiKeyRepository>().GetByIdAsync(daemon.ApiKeyId).Returns(apiKey);

        await sutProvider.Sut.DeleteAsync(daemon.OrganizationId, actingUserId, daemon.Id);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.DaemonDeleted && e.Phase == AccessAuditEventPhase.Attempt
            && e.DaemonId == daemon.Id && e.DaemonName == daemon.Name && e.ActorId == actingUserId));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.DaemonDeleted && e.Phase == AccessAuditEventPhase.Outcome
            && e.DaemonId == daemon.Id));
    }

    private static SutProvider<DeleteDaemonCommand> Setup()
    {
        var sutProvider = new SutProvider<DeleteDaemonCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
