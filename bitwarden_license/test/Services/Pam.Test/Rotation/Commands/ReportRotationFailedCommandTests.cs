using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation;
using Bit.Services.Pam.Rotation.Commands;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Rotation.Commands;

[SutProviderCustomize]
public class ReportRotationFailedCommandTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan _failureRetryDelay = TimeSpan.FromHours(1);

    [Theory, BitAutoData]
    public async Task ReportFailedAsync_UnknownAttempt_ThrowsNotFound_NoAudit(
        Guid daemonId, Guid attemptId, string failureReason, PamRotationSyncState syncState)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationJobRepository>().GetAttemptByIdAsync(attemptId)
            .Returns((PamRotationAttempt?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.ReportFailedAsync(daemonId, attemptId, failureReason, syncState));

        await sutProvider.GetDependency<IPamRotationJobRepository>().DidNotReceiveWithAnyArgs()
            .MarkAttemptErroredAsync(default, default, default, default, default, default, default);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs().EmitAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task ReportFailedAsync_ReasonExceeds500Chars_TruncatesBeforeRepositoryCall(
        Guid daemonId, PamRotationAttempt attempt, PamRotationJob job, PamRotationConfig config,
        PamDaemon daemon, PamRotationSyncState syncState)
    {
        var sutProvider = Setup();
        job.RotationConfigId = config.Id;
        attempt.JobId = job.Id;
        var longReason = new string('x', 600);
        var expectedTruncated = longReason.Substring(0, 500);
        SetupAttempt(sutProvider, attempt);
        sutProvider.GetDependency<IPamRotationJobRepository>()
            .MarkAttemptErroredAsync(attempt.Id, daemonId, Arg.Any<string>(), syncState, _now, Arg.Any<int>(), Arg.Any<TimeSpan>())
            .Returns(new PamRotationFailureResult { Outcome = PamRotationAttemptResolveOutcome.Resolved, JobStatus = PamRotationJobStatus.Pending });
        sutProvider.GetDependency<IPamRotationJobRepository>().GetByIdAsync(job.Id).Returns(job);
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemonId).Returns(daemon);

        await sutProvider.Sut.ReportFailedAsync(daemonId, attempt.Id, longReason, syncState);

        await sutProvider.GetDependency<IPamRotationJobRepository>().Received(1).MarkAttemptErroredAsync(
            attempt.Id, daemonId, Arg.Is<string>(s => s.Length == 500 && s == expectedTruncated), syncState, _now,
            Arg.Any<int>(), Arg.Any<TimeSpan>());
    }

    [Theory, BitAutoData]
    public async Task ReportFailedAsync_RetryBudgetRemains_EmitsAttemptFailedAuditAndConfigUntouched(
        Guid daemonId, PamRotationAttempt attempt, PamRotationJob job, PamRotationConfig config, PamDaemon daemon,
        string failureReason, PamRotationSyncState syncState)
    {
        var sutProvider = Setup();
        job.RotationConfigId = config.Id;
        attempt.JobId = job.Id;
        SetupAttempt(sutProvider, attempt);
        sutProvider.GetDependency<IPamRotationJobRepository>()
            .MarkAttemptErroredAsync(attempt.Id, daemonId, Arg.Any<string>(), syncState, _now, Arg.Any<int>(), Arg.Any<TimeSpan>())
            .Returns(new PamRotationFailureResult { Outcome = PamRotationAttemptResolveOutcome.Resolved, JobStatus = PamRotationJobStatus.Pending });
        sutProvider.GetDependency<IPamRotationJobRepository>().GetByIdAsync(job.Id).Returns(job);
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemonId).Returns(daemon);

        await sutProvider.Sut.ReportFailedAsync(daemonId, attempt.Id, failureReason, syncState);

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default!);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(1).EmitAsync(
            Arg.Is<AccessAuditEventData>(a => a.Kind == AccessAuditEventKind.RotationAttemptFailed
                && a.OrganizationId == config.OrganizationId
                && a.DaemonId == daemonId
                && a.RotationJobId == job.Id
                && a.RotationConfigId == config.Id
                && a.SyncState == syncState
                && a.Detail == failureReason));
    }

    [Theory, BitAutoData]
    public async Task ReportFailedAsync_RetryBudgetExhausted_UpdatesConfigNextRotationAndEmitsFailedAudit(
        Guid daemonId, PamRotationAttempt attempt, PamRotationJob job, PamRotationConfig config, PamDaemon daemon,
        string failureReason, PamRotationSyncState syncState)
    {
        var sutProvider = Setup();
        job.RotationConfigId = config.Id;
        attempt.JobId = job.Id;
        SetupAttempt(sutProvider, attempt);
        sutProvider.GetDependency<IPamRotationJobRepository>()
            .MarkAttemptErroredAsync(attempt.Id, daemonId, Arg.Any<string>(), syncState, _now, Arg.Any<int>(), Arg.Any<TimeSpan>())
            .Returns(new PamRotationFailureResult { Outcome = PamRotationAttemptResolveOutcome.Resolved, JobStatus = PamRotationJobStatus.Failed });
        sutProvider.GetDependency<IPamRotationJobRepository>().GetByIdAsync(job.Id).Returns(job);
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemonId).Returns(daemon);

        await sutProvider.Sut.ReportFailedAsync(daemonId, attempt.Id, failureReason, syncState);

        await sutProvider.GetDependency<IPamRotationConfigRepository>().Received(1).ReplaceAsync(
            Arg.Is<PamRotationConfig>(c => c.Id == config.Id && c.NextRotationAt == _now + _failureRetryDelay));
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(1).EmitAsync(
            Arg.Is<AccessAuditEventData>(a => a.Kind == AccessAuditEventKind.RotationFailed
                && a.OrganizationId == config.OrganizationId
                && a.DaemonId == daemonId
                && a.RotationJobId == job.Id
                && a.RotationConfigId == config.Id
                && a.SyncState == syncState
                && a.Detail == failureReason));
    }

    [Theory, BitAutoData]
    public async Task ReportFailedAsync_Rejected_EmitsReportRejectedAuditAndThrowsConflict_ConfigNotUpdated(
        Guid daemonId, PamRotationAttempt attempt, PamRotationJob job, PamRotationConfig config,
        string failureReason, PamRotationSyncState syncState)
    {
        var sutProvider = Setup();
        job.RotationConfigId = config.Id;
        attempt.JobId = job.Id;
        SetupAttempt(sutProvider, attempt);
        sutProvider.GetDependency<IPamRotationJobRepository>()
            .MarkAttemptErroredAsync(attempt.Id, daemonId, Arg.Any<string>(), syncState, _now, Arg.Any<int>(), Arg.Any<TimeSpan>())
            .Returns(new PamRotationFailureResult { Outcome = PamRotationAttemptResolveOutcome.Rejected, JobStatus = null });
        sutProvider.GetDependency<IPamRotationJobRepository>().GetByIdAsync(job.Id).Returns(job);
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);

        await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.ReportFailedAsync(daemonId, attempt.Id, failureReason, syncState));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default!);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(1).EmitAsync(
            Arg.Is<AccessAuditEventData>(a => a.Kind == AccessAuditEventKind.RotationReportRejected
                && a.OrganizationId == config.OrganizationId
                && a.DaemonId == daemonId
                && a.RotationJobId == job.Id
                && a.RotationConfigId == config.Id));
    }

    private static SutProvider<ReportRotationFailedCommand> Setup()
    {
        var sutProvider = new SutProvider<ReportRotationFailedCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        sutProvider.GetDependency<IOptions<PamRotationOptions>>().Value
            .Returns(new PamRotationOptions { FailureRetryDelay = _failureRetryDelay });
        return sutProvider;
    }

    private static void SetupAttempt(SutProvider<ReportRotationFailedCommand> sutProvider, PamRotationAttempt attempt) =>
        sutProvider.GetDependency<IPamRotationJobRepository>().GetAttemptByIdAsync(attempt.Id).Returns(attempt);
}
