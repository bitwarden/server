using Bit.Core.Entities;
using Bit.Core.Pam.Services;
using Bit.Core.Repositories;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Services;

public class LeasingBypassAuditTests
{
    private static readonly DateTime _now = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid _organizationId = Guid.NewGuid();
    private static readonly Guid _gatingCollectionId = Guid.NewGuid();

    [Fact]
    public async Task AuditCollectionChangeAsync_NothingGatedBefore_RunsTheChangeAndRecordsNothing()
    {
        var sutProvider = Setup();
        GatedBeforeAndAfter(sutProvider, before: [], after: []);
        var ran = false;

        await sutProvider.Sut.AuditCollectionChangeAsync(_organizationId, Guid.NewGuid(), [Guid.NewGuid()],
            () =>
            {
                ran = true;
                return Task.CompletedTask;
            });

        Assert.True(ran);
        await sutProvider.GetDependency<ICipherLeaseGate>().Received(1)
            .GetOrganizationGatedCipherIdsAsync(_organizationId, Arg.Any<IEnumerable<Guid>>());
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs().EmitAsync(default!);
    }

    [Fact]
    public async Task AuditCollectionChangeAsync_AsksTheGateBeforeAndAfterTheChange()
    {
        var sutProvider = Setup();
        var cipherId = Guid.NewGuid();
        var calls = new List<string>();
        sutProvider.GetDependency<ICipherLeaseGate>()
            .GetOrganizationGatedCipherIdsAsync(_organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(new HashSet<Guid> { cipherId })
            .AndDoes(_ => calls.Add("gate"));

        await sutProvider.Sut.AuditCollectionChangeAsync(_organizationId, Guid.NewGuid(), [cipherId],
            () =>
            {
                calls.Add("change");
                return Task.CompletedTask;
            });

        Assert.Equal(["gate", "change", "gate"], calls);
    }

    [Fact]
    public async Task AuditCollectionChangeAsync_ChangeThrows_RecordsNothing()
    {
        var sutProvider = Setup();
        var cipherId = Guid.NewGuid();
        GatedBeforeAndAfter(sutProvider, before: [cipherId], after: []);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sutProvider.Sut.AuditCollectionChangeAsync(_organizationId, Guid.NewGuid(), [cipherId],
                () => throw new InvalidOperationException()));

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs().EmitAsync(default!);
    }

    [Fact]
    public async Task AuditCollectionChangeAsync_StillGated_RecordsNothing()
    {
        var sutProvider = Setup();
        var cipherId = Guid.NewGuid();
        GatedBeforeAndAfter(sutProvider, before: [cipherId], after: [cipherId]);

        await sutProvider.Sut.AuditCollectionChangeAsync(_organizationId, Guid.NewGuid(), [cipherId],
            () => Task.CompletedTask);

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs().EmitAsync(default!);
    }

    [Fact]
    public async Task AuditCollectionChangeAsync_OneCollectionAdded_RecordsItWithTheEvent()
    {
        var sutProvider = Setup();
        var cipherId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var addedCollectionId = Guid.NewGuid();
        GatedBeforeAndAfter(sutProvider, before: [cipherId], after: []);
        Links(sutProvider, (cipherId, _gatingCollectionId), (cipherId, addedCollectionId));

        await sutProvider.Sut.AuditCollectionChangeAsync(_organizationId, actingUserId, [cipherId],
            () => Task.CompletedTask);

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(1).EmitAsync(
            Arg.Is<AccessAuditEventData>(e =>
                e.Kind == AccessAuditEventKind.CredentialUngated &&
                e.Phase == AccessAuditEventPhase.Outcome &&
                e.OccurredDate == _now &&
                e.OrganizationId == _organizationId &&
                e.ActorId == actingUserId &&
                e.CipherId == cipherId &&
                e.CollectionId == addedCollectionId));
    }

    [Fact]
    public async Task AuditCollectionChangeAsync_SeveralCollectionsAdded_RecordsNoCollection()
    {
        var sutProvider = Setup();
        var cipherId = Guid.NewGuid();
        GatedBeforeAndAfter(sutProvider, before: [cipherId], after: []);
        Links(sutProvider, (cipherId, Guid.NewGuid()), (cipherId, Guid.NewGuid()));

        await sutProvider.Sut.AuditCollectionChangeAsync(_organizationId, Guid.NewGuid(), [cipherId],
            () => Task.CompletedTask);

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(1).EmitAsync(
            Arg.Is<AccessAuditEventData>(e => e.CipherId == cipherId && e.CollectionId == null));
    }

    [Fact]
    public async Task AuditCollectionChangeAsync_LeftInNoCollection_RecordsNoCollection()
    {
        var sutProvider = Setup();
        var cipherId = Guid.NewGuid();
        GatedBeforeAndAfter(sutProvider, before: [cipherId], after: []);
        Links(sutProvider);

        await sutProvider.Sut.AuditCollectionChangeAsync(_organizationId, Guid.NewGuid(), [cipherId],
            () => Task.CompletedTask);

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(1).EmitAsync(
            Arg.Is<AccessAuditEventData>(e => e.CipherId == cipherId && e.CollectionId == null));
    }

    [Fact]
    public async Task AuditCollectionChangeAsync_ManyCiphers_RecordsOnlyTheOnesNoLongerGated()
    {
        var sutProvider = Setup();
        var ungatedId = Guid.NewGuid();
        var stillGatedId = Guid.NewGuid();
        GatedBeforeAndAfter(sutProvider, before: [ungatedId, stillGatedId], after: [stillGatedId]);
        Links(sutProvider, (ungatedId, Guid.NewGuid()));

        await sutProvider.Sut.AuditCollectionChangeAsync(_organizationId, Guid.NewGuid(), [ungatedId, stillGatedId],
            () => Task.CompletedTask);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Any<AccessAuditEventData>());
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e => e.CipherId == ungatedId));
    }

    [Fact]
    public async Task AuditWholeVaultExportAsync_CarryingAGatedCipher_RecordsTheExport()
    {
        var sutProvider = Setup();
        var actingUserId = Guid.NewGuid();
        var gatedId = Guid.NewGuid();
        OrganizationGated(sutProvider, gatedId);

        await sutProvider.Sut.AuditWholeVaultExportAsync(_organizationId, actingUserId, [gatedId, Guid.NewGuid()]);

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(1).EmitAsync(
            Arg.Is<AccessAuditEventData>(e =>
                e.Kind == AccessAuditEventKind.VaultExported &&
                e.Phase == AccessAuditEventPhase.Outcome &&
                e.OccurredDate == _now &&
                e.OrganizationId == _organizationId &&
                e.ActorId == actingUserId &&
                e.CipherId == null));
    }

    [Fact]
    public async Task AuditWholeVaultExportAsync_CarryingNoGatedCipher_RecordsNothing()
    {
        var sutProvider = Setup();
        // The organization gates a cipher the export doesn't carry.
        OrganizationGated(sutProvider, Guid.NewGuid());

        await sutProvider.Sut.AuditWholeVaultExportAsync(_organizationId, Guid.NewGuid(), [Guid.NewGuid()]);

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs().EmitAsync(default!);
        await sutProvider.GetDependency<ICipherLeaseGate>().DidNotReceiveWithAnyArgs()
            .GetOrganizationGatedCipherIdsAsync(default, default!);
    }

    private static SutProvider<LeasingBypassAudit> Setup()
    {
        var sutProvider = new SutProvider<LeasingBypassAudit>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        sutProvider.GetDependency<IGatingCollectionResolver>().GetGatingCollectionIdsAsync(_organizationId)
            .Returns(new HashSet<Guid> { _gatingCollectionId });
        return sutProvider;
    }

    /// <summary>
    /// The gate's answer on its first call, before the change, and on its second, after it.
    /// </summary>
    private static void GatedBeforeAndAfter(SutProvider<LeasingBypassAudit> sutProvider, Guid[] before, Guid[] after) =>
        sutProvider.GetDependency<ICipherLeaseGate>()
            .GetOrganizationGatedCipherIdsAsync(_organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(before.ToHashSet(), after.ToHashSet());

    private static void OrganizationGated(SutProvider<LeasingBypassAudit> sutProvider, params Guid[] gatedCipherIds) =>
        sutProvider.GetDependency<ICipherLeaseGate>().GetOrganizationGatedCipherIdsForExportAsync(_organizationId)
            .Returns(gatedCipherIds.ToHashSet());

    private static void Links(SutProvider<LeasingBypassAudit> sutProvider,
        params (Guid CipherId, Guid CollectionId)[] links) =>
        sutProvider.GetDependency<ICollectionCipherRepository>().GetManyByCipherIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(links.Select(l => new CollectionCipher { CipherId = l.CipherId, CollectionId = l.CollectionId })
                .ToList());
}
