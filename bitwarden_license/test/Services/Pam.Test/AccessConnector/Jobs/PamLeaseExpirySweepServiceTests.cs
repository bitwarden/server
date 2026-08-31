using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.AccessConnector.Jobs;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.AccessConnector.Jobs;

[SutProviderCustomize]
public class PamLeaseExpirySweepServiceTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SweepAsync_PerExpiredLease_EmitsLeaseExpiredAuditAndCallsHandleAccessGrantEnded()
    {
        var sutProvider = Setup();
        var lease1 = MakeExpiredLease();
        var lease2 = MakeExpiredLease();
        sutProvider.GetDependency<IAccessLeaseRepository>().ExpireDueAsync(_now)
            .Returns(new List<PamExpiredLease> { lease1, lease2 });

        await sutProvider.Sut.SweepAsync();

        foreach (var lease in new[] { lease1, lease2 })
        {
            await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(1).EmitAsync(
                Arg.Is<AccessAuditEventData>(a => a.Kind == AccessAuditEventKind.LeaseExpired
                    && a.OrganizationId == lease.OrganizationId
                    && a.ActorId == null
                    && a.RequesterId == lease.RequesterId
                    && a.CollectionId == lease.CollectionId
                    && a.CipherId == lease.CipherId
                    && a.AccessLeaseId == lease.Id
                    && a.LeaseNotBefore == lease.NotBefore
                    && a.LeaseNotAfter == lease.NotAfter));
            await sutProvider.GetDependency<IHandleAccessGrantEndedCommand>().Received(1)
                .HandleAsync(lease.CipherId);
        }
    }

    [Fact]
    public async Task SweepAsync_OneLeaseThrows_ContinuesWithOthers()
    {
        var sutProvider = Setup();
        var lease1 = MakeExpiredLease();
        var lease2 = MakeExpiredLease();
        sutProvider.GetDependency<IAccessLeaseRepository>().ExpireDueAsync(_now)
            .Returns(new List<PamExpiredLease> { lease1, lease2 });
        sutProvider.GetDependency<IHandleAccessGrantEndedCommand>().HandleAsync(lease1.CipherId)
            .Returns(Task.FromException(new InvalidOperationException("boom")));

        // lease1's failure (raised from the HandleAsync call the sweep awaits after its own audit emit) must be
        // logged and swallowed per-lease, never preventing lease2 from being processed.
        await sutProvider.Sut.SweepAsync();

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(1).EmitAsync(
            Arg.Is<AccessAuditEventData>(a => a.AccessLeaseId == lease2.Id));
        await sutProvider.GetDependency<IHandleAccessGrantEndedCommand>().Received(1).HandleAsync(lease2.CipherId);
    }

    [Fact]
    public async Task SweepAsync_AuditEmitThrows_StillFiresTheAccessEndTrigger()
    {
        var sutProvider = Setup();
        var lease = MakeExpiredLease();
        sutProvider.GetDependency<IAccessLeaseRepository>().ExpireDueAsync(_now)
            .Returns(new List<PamExpiredLease> { lease });
        sutProvider.GetDependency<IAccessAuditEventEmitter>()
            .EmitAsync(Arg.Any<AccessAuditEventData>())
            .Returns(Task.FromException(new InvalidOperationException("audit store unavailable")));

        await sutProvider.Sut.SweepAsync();

        // ExpireDueAsync has already flipped the lease out of Active, so it is never returned again -- letting an
        // audit hiccup swallow the rotation trigger would silently leave the credential the user just held valid.
        await sutProvider.GetDependency<IHandleAccessGrantEndedCommand>().Received(1).HandleAsync(lease.CipherId);
    }

    private static SutProvider<PamLeaseExpirySweepService> Setup()
    {
        var sutProvider = new SutProvider<PamLeaseExpirySweepService>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }

    private static PamExpiredLease MakeExpiredLease() => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = Guid.NewGuid(),
        CollectionId = Guid.NewGuid(),
        CipherId = Guid.NewGuid(),
        RequesterId = Guid.NewGuid(),
        NotBefore = _now.AddHours(-2),
        NotAfter = _now.AddMinutes(-1),
    };
}
