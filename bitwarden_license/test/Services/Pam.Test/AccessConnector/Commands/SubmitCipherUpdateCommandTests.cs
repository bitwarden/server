using Bit.Core.Exceptions;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
using Bit.Core.Vault.Services;
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
public class SubmitCipherUpdateCommandTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task SubmitAsync_UnknownAttempt_ThrowsNotFound_NoAudit_NoPush(
        Guid accessConnectorId, Guid attemptId, string cipherDataJson, DateTime lastKnownRevisionDate)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationJobRepository>().GetAttemptByIdAsync(attemptId)
            .Returns((PamRotationAttempt?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.SubmitAsync(accessConnectorId, attemptId, cipherDataJson, lastKnownRevisionDate));

        await sutProvider.GetDependency<IPamRotationJobRepository>().DidNotReceiveWithAnyArgs()
            .AcceptCipherWriteAsync(default, default, default!, default, default);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs().EmitAsync(default!);
        await sutProvider.GetDependency<ICipherSyncPushService>().DidNotReceiveWithAnyArgs()
            .PushSyncCipherUpdateAsync(default!, default!);
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_Accepted_PushesCipherSyncUpdate(
        Guid accessConnectorId, PamRotationAttempt attempt, PamRotationJob job, PamRotationConfig config,
        PamAccessConnector accessConnector, Cipher cipher, string cipherDataJson, DateTime lastKnownRevisionDate)
    {
        var sutProvider = Setup();
        job.RotationConfigId = config.Id;
        attempt.JobId = job.Id;
        cipher.Id = config.CipherId;
        accessConnector.OrganizationId = config.OrganizationId;
        SetupAttempt(sutProvider, attempt);
        sutProvider.GetDependency<IPamRotationJobRepository>()
            .AcceptCipherWriteAsync(attempt.Id, accessConnectorId, cipherDataJson, lastKnownRevisionDate, _now)
            .Returns(PamRotationCipherWriteOutcome.Accepted);
        sutProvider.GetDependency<IPamRotationJobRepository>().GetByIdAsync(job.Id).Returns(job);
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnectorId)
            .Returns(accessConnector);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(config.CipherId).Returns(cipher);

        await sutProvider.Sut.SubmitAsync(accessConnectorId, attempt.Id, cipherDataJson, lastKnownRevisionDate);

        await sutProvider.GetDependency<ICipherSyncPushService>().Received(1)
            .PushSyncCipherUpdateAsync(cipher, Arg.Is<IEnumerable<Guid>>(c => !c.Any()));
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs().EmitAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_Rejected_EmitsWriteRejectedAuditAndThrowsConflict_PushNotCalled(
        Guid accessConnectorId, PamRotationAttempt attempt, PamRotationJob job, PamRotationConfig config,
        PamAccessConnector accessConnector, string cipherDataJson, DateTime lastKnownRevisionDate)
    {
        var sutProvider = Setup();
        job.RotationConfigId = config.Id;
        attempt.JobId = job.Id;
        accessConnector.OrganizationId = config.OrganizationId;
        SetupAttempt(sutProvider, attempt);
        sutProvider.GetDependency<IPamRotationJobRepository>()
            .AcceptCipherWriteAsync(attempt.Id, accessConnectorId, cipherDataJson, lastKnownRevisionDate, _now)
            .Returns(PamRotationCipherWriteOutcome.Rejected);
        sutProvider.GetDependency<IPamRotationJobRepository>().GetByIdAsync(job.Id).Returns(job);
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnectorId)
            .Returns(accessConnector);

        await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.SubmitAsync(accessConnectorId, attempt.Id, cipherDataJson, lastKnownRevisionDate));

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(1).EmitAsync(
            Arg.Is<AccessAuditEventData>(a => a.Kind == AccessAuditEventKind.RotationCipherWriteRejected
                && a.OrganizationId == config.OrganizationId
                && a.AccessConnectorId == accessConnectorId
                && a.RotationJobId == job.Id
                && a.RotationConfigId == config.Id
                && a.CipherId == config.CipherId));
        await sutProvider.GetDependency<ICipherSyncPushService>().DidNotReceiveWithAnyArgs()
            .PushSyncCipherUpdateAsync(default!, default!);
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_RevisionMismatch_EmitsWriteRejectedAuditAndThrowsConflict_PushNotCalled(
        Guid accessConnectorId, PamRotationAttempt attempt, PamRotationJob job, PamRotationConfig config,
        PamAccessConnector accessConnector, string cipherDataJson, DateTime lastKnownRevisionDate)
    {
        var sutProvider = Setup();
        job.RotationConfigId = config.Id;
        attempt.JobId = job.Id;
        accessConnector.OrganizationId = config.OrganizationId;
        SetupAttempt(sutProvider, attempt);
        sutProvider.GetDependency<IPamRotationJobRepository>()
            .AcceptCipherWriteAsync(attempt.Id, accessConnectorId, cipherDataJson, lastKnownRevisionDate, _now)
            .Returns(PamRotationCipherWriteOutcome.RevisionMismatch);
        sutProvider.GetDependency<IPamRotationJobRepository>().GetByIdAsync(job.Id).Returns(job);
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnectorId)
            .Returns(accessConnector);

        await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.SubmitAsync(accessConnectorId, attempt.Id, cipherDataJson, lastKnownRevisionDate));

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(1).EmitAsync(
            Arg.Is<AccessAuditEventData>(a => a.Kind == AccessAuditEventKind.RotationCipherWriteRejected
                && a.RotationJobId == job.Id));
        await sutProvider.GetDependency<ICipherSyncPushService>().DidNotReceiveWithAnyArgs()
            .PushSyncCipherUpdateAsync(default!, default!);
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_AttemptInAnotherOrganization_ThrowsNotFound_NoWriteNoAudit(
        Guid accessConnectorId, PamRotationAttempt attempt, PamRotationJob job, PamRotationConfig config,
        PamAccessConnector accessConnector, string cipherDataJson, DateTime lastKnownRevisionDate)
    {
        var sutProvider = Setup();
        job.RotationConfigId = config.Id;
        attempt.JobId = job.Id;
        accessConnector.OrganizationId = Guid.NewGuid();
        config.OrganizationId = Guid.NewGuid();
        SetupAttempt(sutProvider, attempt);
        sutProvider.GetDependency<IPamRotationJobRepository>().GetByIdAsync(job.Id).Returns(job);
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnectorId)
            .Returns(accessConnector);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.SubmitAsync(accessConnectorId, attempt.Id, cipherDataJson, lastKnownRevisionDate));

        await sutProvider.GetDependency<IPamRotationJobRepository>().DidNotReceiveWithAnyArgs()
            .AcceptCipherWriteAsync(default, default, default!, default, default);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs().EmitAsync(default!);
    }

    private static SutProvider<SubmitCipherUpdateCommand> Setup()
    {
        var sutProvider = new SutProvider<SubmitCipherUpdateCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }

    private static void SetupAttempt(SutProvider<SubmitCipherUpdateCommand> sutProvider, PamRotationAttempt attempt) =>
        sutProvider.GetDependency<IPamRotationJobRepository>().GetAttemptByIdAsync(attempt.Id).Returns(attempt);
}
