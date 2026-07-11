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
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Rotation.Commands;

[SutProviderCustomize]
public class ReportRotationSucceededCommandTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _nextOccurrence = new(2026, 6, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task ReportSucceededAsync_UnknownAttempt_ThrowsNotFound_NoAudit(
        Guid daemonId, Guid attemptId, PamSessionTerminationOutcome sessionTermination)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationJobRepository>().GetAttemptByIdAsync(attemptId)
            .Returns((PamRotationAttempt?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.ReportSucceededAsync(daemonId, attemptId, sessionTermination));

        await sutProvider.GetDependency<IPamRotationJobRepository>().DidNotReceiveWithAnyArgs()
            .MarkAttemptRotatedAsync(default, default, default, default);
        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default!);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs().EmitAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task ReportSucceededAsync_Resolved_UpdatesConfigAndEmitsRotationSucceededAudit(
        Guid daemonId, PamRotationAttempt attempt, PamRotationJob job, PamRotationConfig config, PamDaemon daemon,
        PamSessionTerminationOutcome sessionTermination)
    {
        var sutProvider = Setup();
        job.RotationConfigId = config.Id;
        attempt.JobId = job.Id;
        daemon.Id = daemonId;
        SetupAttempt(sutProvider, attempt);
        sutProvider.GetDependency<IPamRotationJobRepository>()
            .MarkAttemptRotatedAsync(attempt.Id, daemonId, sessionTermination, _now)
            .Returns(PamRotationAttemptResolveOutcome.Resolved);
        sutProvider.GetDependency<IPamRotationJobRepository>().GetByIdAsync(job.Id).Returns(job);
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemonId).Returns(daemon);
        sutProvider.GetDependency<IRotationScheduleCalculator>().GetNextOccurrence(config.ScheduleCron, _now)
            .Returns(_nextOccurrence);

        var returned = await sutProvider.Sut.ReportSucceededAsync(daemonId, attempt.Id, sessionTermination);

        Assert.Same(attempt, returned);
        await sutProvider.GetDependency<IPamRotationConfigRepository>().Received(1).ReplaceAsync(
            Arg.Is<PamRotationConfig>(c => c.Id == config.Id
                && c.LastRotationAt == _now
                && c.NextRotationAt == _nextOccurrence));
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(1).EmitAsync(
            Arg.Is<AccessAuditEventData>(a => a.Kind == AccessAuditEventKind.RotationSucceeded
                && a.OrganizationId == config.OrganizationId
                && a.ActorId == null
                && a.DaemonId == daemonId
                && a.DaemonName == daemon.Name
                && a.RotationJobId == job.Id
                && a.RotationConfigId == config.Id
                && a.CipherId == config.CipherId
                && a.RotationSource == job.Source));
    }

    [Theory, BitAutoData]
    public async Task ReportSucceededAsync_Rejected_EmitsReportRejectedAuditAndThrowsConflict_ConfigNotUpdated(
        Guid daemonId, PamRotationAttempt attempt, PamRotationJob job, PamRotationConfig config,
        PamSessionTerminationOutcome sessionTermination)
    {
        var sutProvider = Setup();
        job.RotationConfigId = config.Id;
        attempt.JobId = job.Id;
        SetupAttempt(sutProvider, attempt);
        sutProvider.GetDependency<IPamRotationJobRepository>()
            .MarkAttemptRotatedAsync(attempt.Id, daemonId, sessionTermination, _now)
            .Returns(PamRotationAttemptResolveOutcome.Rejected);
        sutProvider.GetDependency<IPamRotationJobRepository>().GetByIdAsync(job.Id).Returns(job);
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);

        await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.ReportSucceededAsync(daemonId, attempt.Id, sessionTermination));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default!);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(1).EmitAsync(
            Arg.Is<AccessAuditEventData>(a => a.Kind == AccessAuditEventKind.RotationReportRejected
                && a.OrganizationId == config.OrganizationId
                && a.DaemonId == daemonId
                && a.RotationJobId == job.Id
                && a.RotationConfigId == config.Id
                && a.CipherId == config.CipherId));
    }

    private static SutProvider<ReportRotationSucceededCommand> Setup()
    {
        var sutProvider = new SutProvider<ReportRotationSucceededCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }

    private static void SetupAttempt(SutProvider<ReportRotationSucceededCommand> sutProvider, PamRotationAttempt attempt) =>
        sutProvider.GetDependency<IPamRotationJobRepository>().GetAttemptByIdAsync(attempt.Id).Returns(attempt);
}
