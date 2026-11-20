using Bit.Core;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bitwarden.Server.Sdk.Features;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Services;

public class LeasingBypassAuditTests
{
    private static readonly DateTime _now = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid _organizationId = Guid.NewGuid();
    private static readonly Guid _gatingCollectionId = Guid.NewGuid();
    private static readonly Guid _plainCollectionId = Guid.NewGuid();

    [Fact]
    public async Task GetGatedCipherIdsAsync_FlagOff_ReturnsNoneWithoutQuerying()
    {
        var sutProvider = Setup(enabled: false);

        var gated = await sutProvider.Sut.GetGatedCipherIdsAsync(_organizationId, [Guid.NewGuid()]);

        Assert.Empty(gated);
        await sutProvider.GetDependency<IGatingCollectionResolver>().DidNotReceiveWithAnyArgs()
            .GetGatingCollectionIdsAsync(default);
    }

    [Fact]
    public async Task GetGatedCipherIdsAsync_NoGatingCollection_ReturnsNoneWithoutReadingCollections()
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IGatingCollectionResolver>().GetGatingCollectionIdsAsync(_organizationId)
            .Returns(new HashSet<Guid>());

        var gated = await sutProvider.Sut.GetGatedCipherIdsAsync(_organizationId, [Guid.NewGuid()]);

        Assert.Empty(gated);
        await sutProvider.GetDependency<ICollectionCipherRepository>().DidNotReceiveWithAnyArgs()
            .GetCollectionIdsByCipherIdAsync(default);
    }

    [Fact]
    public async Task GetGatedCipherIdsAsync_OrganizationWithoutPrivilegedControls_ReturnsNoneWithoutQuerying()
    {
        var sutProvider = Setup(usePam: false);

        var gated = await sutProvider.Sut.GetGatedCipherIdsAsync(_organizationId, [Guid.NewGuid()]);

        Assert.Empty(gated);
        await sutProvider.GetDependency<IGatingCollectionResolver>().DidNotReceiveWithAnyArgs()
            .GetGatingCollectionIdsAsync(default);
    }

    [Fact]
    public async Task GetGatedCipherIdsAsync_UnknownOrganization_StillChecksItsRules()
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(_organizationId, Arg.Any<CancellationToken>())
            .Returns((OrganizationAbility?)null);
        var cipherId = Guid.NewGuid();
        InCollections(sutProvider, cipherId, [_gatingCollectionId]);

        var gated = await sutProvider.Sut.GetGatedCipherIdsAsync(_organizationId, [cipherId]);

        Assert.Equal([cipherId], gated);
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    public async Task GetGatedCipherIdsAsync_OneCipher_IsGatedOnlyThroughGatingCollectionsAlone(
        bool inGating, bool inPlain, bool expectedGated)
    {
        var sutProvider = Setup();
        var cipherId = Guid.NewGuid();
        InCollections(sutProvider, cipherId,
            [.. inGating ? [_gatingCollectionId] : Array.Empty<Guid>(), .. inPlain ? [_plainCollectionId] : Array.Empty<Guid>()]);

        var gated = await sutProvider.Sut.GetGatedCipherIdsAsync(_organizationId, [cipherId]);

        Assert.Equal(expectedGated, gated.Contains(cipherId));
        await sutProvider.GetDependency<ICollectionCipherRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdAsync(default);
    }

    [Fact]
    public async Task GetGatedCipherIdsAsync_ManyCiphers_ReadsTheOrganizationOnceAndKeepsOnlyTheRequested()
    {
        var sutProvider = Setup();
        var gatedId = Guid.NewGuid();
        var alsoPlainId = Guid.NewGuid();
        var unassignedId = Guid.NewGuid();
        var notRequestedId = Guid.NewGuid();
        OrganizationLinks(sutProvider,
            (gatedId, _gatingCollectionId),
            (alsoPlainId, _gatingCollectionId),
            (alsoPlainId, _plainCollectionId),
            (notRequestedId, _gatingCollectionId));

        var gated = await sutProvider.Sut.GetGatedCipherIdsAsync(_organizationId, [gatedId, alsoPlainId, unassignedId]);

        Assert.Equal([gatedId], gated);
        await sutProvider.GetDependency<ICollectionCipherRepository>().DidNotReceiveWithAnyArgs()
            .GetCollectionIdsByCipherIdAsync(default);
    }

    [Fact]
    public async Task AuditUngatedAsync_NothingWasGated_EmitsNothingWithoutQuerying()
    {
        var sutProvider = Setup();

        await sutProvider.Sut.AuditUngatedAsync(_organizationId, Guid.NewGuid(), new HashSet<Guid>());

        await sutProvider.GetDependency<IGatingCollectionResolver>().DidNotReceiveWithAnyArgs()
            .GetGatingCollectionIdsAsync(default);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs().EmitAsync(default!);
    }

    [Fact]
    public async Task AuditUngatedAsync_StillGated_EmitsNothing()
    {
        var sutProvider = Setup();
        var cipherId = Guid.NewGuid();
        InCollections(sutProvider, cipherId, [_gatingCollectionId]);

        await sutProvider.Sut.AuditUngatedAsync(_organizationId, Guid.NewGuid(), new HashSet<Guid> { cipherId });

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs().EmitAsync(default!);
    }

    [Fact]
    public async Task AuditUngatedAsync_GivenAPlainCollection_RecordsTheCollectionThatUngatedIt()
    {
        var sutProvider = Setup();
        var cipherId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        InCollections(sutProvider, cipherId, [_gatingCollectionId, _plainCollectionId]);

        await sutProvider.Sut.AuditUngatedAsync(_organizationId, actingUserId, new HashSet<Guid> { cipherId });

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(1).EmitAsync(
            Arg.Is<AccessAuditEventData>(e =>
                e.Kind == AccessAuditEventKind.CredentialUngated &&
                e.Phase == AccessAuditEventPhase.Outcome &&
                e.OccurredDate == _now &&
                e.OrganizationId == _organizationId &&
                e.ActorId == actingUserId &&
                e.CipherId == cipherId &&
                e.CollectionId == _plainCollectionId));
    }

    [Fact]
    public async Task AuditUngatedAsync_LeftInNoCollection_RecordsNoCollection()
    {
        var sutProvider = Setup();
        var cipherId = Guid.NewGuid();
        InCollections(sutProvider, cipherId, []);

        await sutProvider.Sut.AuditUngatedAsync(_organizationId, Guid.NewGuid(), new HashSet<Guid> { cipherId });

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(1).EmitAsync(
            Arg.Is<AccessAuditEventData>(e =>
                e.Kind == AccessAuditEventKind.CredentialUngated && e.CipherId == cipherId && e.CollectionId == null));
    }

    [Fact]
    public async Task AuditUngatedAsync_ManyCiphers_RecordsOnlyTheOnesNoLongerGated()
    {
        var sutProvider = Setup();
        var ungatedId = Guid.NewGuid();
        var stillGatedId = Guid.NewGuid();
        OrganizationLinks(sutProvider,
            (ungatedId, _gatingCollectionId),
            (ungatedId, _plainCollectionId),
            (stillGatedId, _gatingCollectionId));

        await sutProvider.Sut.AuditUngatedAsync(_organizationId, Guid.NewGuid(),
            new HashSet<Guid> { ungatedId, stillGatedId });

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Any<AccessAuditEventData>());
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e => e.CipherId == ungatedId));
    }

    [Fact]
    public async Task AuditWholeVaultExportAsync_CarryingAGatedCipher_RecordsTheExport()
    {
        var sutProvider = Setup();
        var gatedId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        OrganizationLinks(sutProvider, (gatedId, _gatingCollectionId));

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
        var plainId = Guid.NewGuid();
        OrganizationLinks(sutProvider, (plainId, _plainCollectionId));

        await sutProvider.Sut.AuditWholeVaultExportAsync(_organizationId, Guid.NewGuid(), [plainId, Guid.NewGuid()]);

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs().EmitAsync(default!);
    }

    [Fact]
    public async Task AuditWholeVaultExportAsync_FlagOff_RecordsNothing()
    {
        var sutProvider = Setup(enabled: false);

        await sutProvider.Sut.AuditWholeVaultExportAsync(_organizationId, Guid.NewGuid(), [Guid.NewGuid()]);

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs().EmitAsync(default!);
    }

    [Fact]
    public async Task AuditWholeVaultExportAsync_OrganizationWithoutPrivilegedControls_RecordsNothing()
    {
        var sutProvider = Setup(usePam: false);
        var gatedId = Guid.NewGuid();
        OrganizationLinks(sutProvider, (gatedId, _gatingCollectionId));

        await sutProvider.Sut.AuditWholeVaultExportAsync(_organizationId, Guid.NewGuid(), [gatedId, Guid.NewGuid()]);

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs().EmitAsync(default!);
    }

    private static SutProvider<LeasingBypassAudit> Setup(bool enabled = true, bool usePam = true)
    {
        var sutProvider = new SutProvider<LeasingBypassAudit>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.Pam).Returns(enabled);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(_organizationId, Arg.Any<CancellationToken>())
            .Returns(new OrganizationAbility { UsePam = usePam });
        sutProvider.GetDependency<IGatingCollectionResolver>().GetGatingCollectionIdsAsync(_organizationId)
            .Returns(new HashSet<Guid> { _gatingCollectionId });
        return sutProvider;
    }

    private static void InCollections(SutProvider<LeasingBypassAudit> sutProvider, Guid cipherId, Guid[] collectionIds) =>
        sutProvider.GetDependency<ICollectionCipherRepository>().GetCollectionIdsByCipherIdAsync(cipherId)
            .Returns(collectionIds.ToList());

    private static void OrganizationLinks(SutProvider<LeasingBypassAudit> sutProvider,
        params (Guid CipherId, Guid CollectionId)[] links) =>
        sutProvider.GetDependency<ICollectionCipherRepository>().GetManyByOrganizationIdAsync(_organizationId)
            .Returns(links.Select(l => new CollectionCipher { CipherId = l.CipherId, CollectionId = l.CollectionId })
                .ToList());
}
