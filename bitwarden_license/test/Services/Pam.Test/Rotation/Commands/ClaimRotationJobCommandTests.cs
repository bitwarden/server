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
public class ClaimRotationJobCommandTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan _releaseDelay = TimeSpan.FromMinutes(15);

    [Theory, BitAutoData]
    public async Task ClaimAsync_Claimed_ReturnsSnapshotAndEmitsRotationDispatchedAudit(
        Guid daemonId, Guid jobId, PamDaemon daemon, PamRotationJob job)
    {
        var sutProvider = Setup();
        daemon.Id = daemonId;
        job.Id = jobId;
        var result = new PamRotationClaimResult
        {
            Outcome = PamRotationClaimOutcome.Claimed,
            AttemptId = Guid.NewGuid(),
            JobId = jobId,
            Source = PamRotationSource.Scheduled,
            TargetSystemId = Guid.NewGuid(),
            TargetSystemName = "Prod SQL",
            CipherId = Guid.NewGuid(),
            AccountIdentity = "svc-account",
            TerminateSessions = true,
            ExecuteBy = _now + _releaseDelay,
        };
        sutProvider.GetDependency<IPamRotationJobRepository>()
            .ClaimAsync(jobId, daemonId, _now, _releaseDelay)
            .Returns(result);
        sutProvider.GetDependency<IPamDaemonRepository>().GetByIdAsync(daemonId).Returns(daemon);
        sutProvider.GetDependency<IPamRotationJobRepository>().GetByIdAsync(jobId).Returns(job);

        var returned = await sutProvider.Sut.ClaimAsync(daemonId, jobId);

        Assert.Same(result, returned);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(1).EmitAsync(
            Arg.Is<AccessAuditEventData>(a => a.Kind == AccessAuditEventKind.RotationDispatched
                && a.OrganizationId == daemon.OrganizationId
                && a.ActorId == null
                && a.DaemonId == daemonId
                && a.DaemonName == daemon.Name
                && a.RotationJobId == jobId
                && a.RotationConfigId == job.RotationConfigId
                && a.CipherId == result.CipherId
                && a.TargetSystemId == result.TargetSystemId
                && a.TargetSystemName == result.TargetSystemName
                && a.RotationSource == result.Source));
    }

    [Theory, BitAutoData]
    public async Task ClaimAsync_NotClaimable_ThrowsConflict_NoAudit(Guid daemonId, Guid jobId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationJobRepository>()
            .ClaimAsync(jobId, daemonId, _now, _releaseDelay)
            .Returns(new PamRotationClaimResult { Outcome = PamRotationClaimOutcome.NotClaimable });

        await Assert.ThrowsAsync<ConflictException>(() => sutProvider.Sut.ClaimAsync(daemonId, jobId));

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs().EmitAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task ClaimAsync_NotEligible_ThrowsNotFound_NoAudit(Guid daemonId, Guid jobId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationJobRepository>()
            .ClaimAsync(jobId, daemonId, _now, _releaseDelay)
            .Returns(new PamRotationClaimResult { Outcome = PamRotationClaimOutcome.NotEligible });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.ClaimAsync(daemonId, jobId));

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs().EmitAsync(default!);
    }

    private static SutProvider<ClaimRotationJobCommand> Setup()
    {
        var sutProvider = new SutProvider<ClaimRotationJobCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        sutProvider.GetDependency<IOptions<PamRotationOptions>>().Value
            .Returns(new PamRotationOptions { ReleaseDelay = _releaseDelay });
        return sutProvider;
    }
}
