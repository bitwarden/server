using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation;
using Bit.Services.Pam.Rotation.Commands.Interfaces;
using Bit.Services.Pam.Rotation.Jobs;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Rotation.Jobs;

[SutProviderCustomize]
public class PamRotationSweepServiceTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan _failureRetryDelay = TimeSpan.FromHours(1);

    [Theory, BitAutoData]
    public async Task SweepAsync_DuePhase_OffersEachDueConfig(PamRotationConfig config1, PamRotationConfig config2)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetManyDueAsync(_now)
            .Returns(new List<PamRotationConfig> { config1, config2 });

        await sutProvider.Sut.SweepAsync();

        await sutProvider.GetDependency<IOfferRotationCommand>().Received(1)
            .OfferAsync(config1.Id, PamRotationSource.Scheduled);
        await sutProvider.GetDependency<IOfferRotationCommand>().Received(1)
            .OfferAsync(config2.Id, PamRotationSource.Scheduled);
    }

    [Theory, BitAutoData]
    public async Task SweepAsync_DuePhase_OneConfigThrows_ContinuesWithOthers(
        PamRotationConfig config1, PamRotationConfig config2)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetManyDueAsync(_now)
            .Returns(new List<PamRotationConfig> { config1, config2 });
        sutProvider.GetDependency<IOfferRotationCommand>().OfferAsync(config1.Id, PamRotationSource.Scheduled)
            .Returns(Task.FromException<PamRotationJobCreateOutcome>(new InvalidOperationException("boom")));

        // The due phase's own try/catch (plus the outer RunPhaseAsync) must swallow config1's failure and still
        // process config2 -- and never let the phase's exception escape SweepAsync itself.
        await sutProvider.Sut.SweepAsync();

        await sutProvider.GetDependency<IOfferRotationCommand>().Received(1)
            .OfferAsync(config2.Id, PamRotationSource.Scheduled);
    }

    [Theory, BitAutoData]
    public async Task SweepAsync_TimeoutPhase_UnroutableJob_ReschedulesConfigAndEmitsAuditWithUnroutableDetail(
        PamRotationConfig config)
    {
        var sutProvider = Setup();
        var timedOutJob = new PamTimedOutJob
        {
            JobId = Guid.NewGuid(),
            RotationConfigId = config.Id,
            OrganizationId = config.OrganizationId,
            CipherId = config.CipherId,
            Source = PamRotationSource.Scheduled,
            ClaimedByDaemonId = null,
            AttemptCount = 0,
        };
        sutProvider.GetDependency<IPamRotationJobRepository>().TimeoutDueAsync(_now)
            .Returns(new List<PamTimedOutJob> { timedOutJob });
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);

        await sutProvider.Sut.SweepAsync();

        await sutProvider.GetDependency<IPamRotationConfigRepository>().Received(1).ReplaceAsync(
            Arg.Is<PamRotationConfig>(c => c.Id == config.Id && c.NextRotationAt == _now + _failureRetryDelay));
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(1).EmitAsync(
            Arg.Is<AccessAuditEventData>(a => a.Kind == AccessAuditEventKind.RotationJobTimedOut
                && a.RotationJobId == timedOutJob.JobId
                && a.RotationConfigId == config.Id
                && a.Detail!.Contains("unroutable")));
    }

    [Theory, BitAutoData]
    public async Task SweepAsync_TimeoutPhase_StuckJob_EmitsAuditWithStuckDetail(
        PamRotationConfig config, Guid claimedByDaemonId)
    {
        var sutProvider = Setup();
        var timedOutJob = new PamTimedOutJob
        {
            JobId = Guid.NewGuid(),
            RotationConfigId = config.Id,
            OrganizationId = config.OrganizationId,
            CipherId = config.CipherId,
            Source = PamRotationSource.Scheduled,
            ClaimedByDaemonId = claimedByDaemonId,
            AttemptCount = 3,
        };
        sutProvider.GetDependency<IPamRotationJobRepository>().TimeoutDueAsync(_now)
            .Returns(new List<PamTimedOutJob> { timedOutJob });
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);

        await sutProvider.Sut.SweepAsync();

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(1).EmitAsync(
            Arg.Is<AccessAuditEventData>(a => a.Kind == AccessAuditEventKind.RotationJobTimedOut
                && a.DaemonId == claimedByDaemonId
                && a.Detail!.Contains("stuck")));
    }

    [Theory, BitAutoData]
    public async Task SweepAsync_ReleasePhase_EmitsReleasedAuditPerRow(Guid daemonId1, Guid daemonId2)
    {
        var sutProvider = Setup();
        var released1 = new PamReleasedJob
        {
            JobId = Guid.NewGuid(),
            RotationConfigId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            CipherId = Guid.NewGuid(),
            Source = PamRotationSource.Scheduled,
            ClaimedByDaemonId = daemonId1,
        };
        var released2 = new PamReleasedJob
        {
            JobId = Guid.NewGuid(),
            RotationConfigId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            CipherId = Guid.NewGuid(),
            Source = PamRotationSource.OnDemand,
            ClaimedByDaemonId = daemonId2,
        };
        sutProvider.GetDependency<IPamRotationJobRepository>()
            .ReleaseExpiredLeasesAsync(_now, Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>())
            .Returns(new List<PamReleasedJob> { released1, released2 });

        await sutProvider.Sut.SweepAsync();

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(1).EmitAsync(
            Arg.Is<AccessAuditEventData>(a => a.Kind == AccessAuditEventKind.RotationJobReleased
                && a.RotationJobId == released1.JobId
                && a.RotationConfigId == released1.RotationConfigId
                && a.OrganizationId == released1.OrganizationId
                && a.CipherId == released1.CipherId
                && a.RotationSource == released1.Source
                && a.DaemonId == released1.ClaimedByDaemonId));
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(1).EmitAsync(
            Arg.Is<AccessAuditEventData>(a => a.Kind == AccessAuditEventKind.RotationJobReleased
                && a.RotationJobId == released2.JobId
                && a.DaemonId == released2.ClaimedByDaemonId));
    }

    private static SutProvider<PamRotationSweepService> Setup()
    {
        var sutProvider = new SutProvider<PamRotationSweepService>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        sutProvider.GetDependency<IOptions<PamRotationOptions>>().Value
            .Returns(new PamRotationOptions { FailureRetryDelay = _failureRetryDelay });
        return sutProvider;
    }
}
