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
public class RevokeDaemonCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task RevokeAsync_DaemonMissing_ThrowsNotFound(Guid organizationId, Guid actingUserId, Guid daemonId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemonId).Returns((PamDaemon?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.RevokeAsync(organizationId, actingUserId, daemonId));

        await sutProvider.GetDependency<IPamDaemonRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_WrongOrg_ThrowsNotFound(Guid actingUserId, PamDaemon daemon)
    {
        var sutProvider = Setup();
        daemon.Status = PamDaemonStatus.Enrolled;
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);

        // daemon.OrganizationId is an unrelated AutoFixture Guid -- a cross-org lookup must 404, never leak
        // existence.
        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.RevokeAsync(Guid.NewGuid(), actingUserId, daemon.Id));

        await sutProvider.GetDependency<IPamDaemonRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
        await sutProvider.GetDependency<IApiKeyRepository>().DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_AlreadyRevoked_ThrowsBadRequest(Guid actingUserId, PamDaemon daemon)
    {
        var sutProvider = Setup();
        daemon.Status = PamDaemonStatus.Revoked;
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RevokeAsync(daemon.OrganizationId, actingUserId, daemon.Id));

        await sutProvider.GetDependency<IPamDaemonRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
        await sutProvider.GetDependency<IApiKeyRepository>().DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_Enrolled_RevokesAndDeletesApiKey(Guid actingUserId, PamDaemon daemon, ApiKey apiKey)
    {
        var sutProvider = Setup();
        daemon.Status = PamDaemonStatus.Enrolled;
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);
        sutProvider.GetDependency<IApiKeyRepository>().GetByIdAsync(daemon.ApiKeyId).Returns(apiKey);

        await sutProvider.Sut.RevokeAsync(daemon.OrganizationId, actingUserId, daemon.Id);

        await sutProvider.GetDependency<IPamDaemonRepository>().Received(1).ReplaceAsync(Arg.Is<PamDaemon>(d =>
            d.Id == daemon.Id && d.Status == PamDaemonStatus.Revoked && d.RevisionDate == _now));
        await sutProvider.GetDependency<IApiKeyRepository>().Received(1).DeleteAsync(apiKey);
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_ApiKeyAlreadyGone_StillRevokesWithoutThrowing(Guid actingUserId, PamDaemon daemon)
    {
        var sutProvider = Setup();
        daemon.Status = PamDaemonStatus.Enrolled;
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);
        sutProvider.GetDependency<IApiKeyRepository>().GetByIdAsync(daemon.ApiKeyId).Returns((ApiKey?)null);

        await sutProvider.Sut.RevokeAsync(daemon.OrganizationId, actingUserId, daemon.Id);

        await sutProvider.GetDependency<IPamDaemonRepository>().Received(1).ReplaceAsync(Arg.Any<PamDaemon>());
        await sutProvider.GetDependency<IApiKeyRepository>().DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_Enrolled_EmitsAttemptThenOutcome(Guid actingUserId, PamDaemon daemon, ApiKey apiKey)
    {
        var sutProvider = Setup();
        daemon.Status = PamDaemonStatus.Enrolled;
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemon.Id).Returns(daemon);
        sutProvider.GetDependency<IApiKeyRepository>().GetByIdAsync(daemon.ApiKeyId).Returns(apiKey);

        await sutProvider.Sut.RevokeAsync(daemon.OrganizationId, actingUserId, daemon.Id);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.DaemonRevoked && e.Phase == AccessAuditEventPhase.Attempt
            && e.DaemonId == daemon.Id && e.DaemonName == daemon.Name && e.ActorId == actingUserId));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.DaemonRevoked && e.Phase == AccessAuditEventPhase.Outcome
            && e.DaemonId == daemon.Id));
    }

    private static SutProvider<RevokeDaemonCommand> Setup()
    {
        var sutProvider = new SutProvider<RevokeDaemonCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
