using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Models;
using Bit.Services.Pam.Models.Conditions;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bitwarden.Server.Sdk.Features;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Services;

/// <summary>
/// The read decision, asserted through the interface only. The structural "which ciphers are gated" rule is
/// exercised via <see cref="CipherLeaseGate.AuthorizeReadManyAsync(Guid, IEnumerable{Cipher}, IEnumerable{CollectionDetails}, IDictionary{Guid, IGrouping{Guid, CollectionCipher}})" />
/// rather than a public helper, so these tests stay pinned to what callers can actually reach.
/// </summary>
public class CipherLeaseGateTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

    // --- AuthorizeReadAsync ------------------------------------------------------------------------

    [Fact]
    public async Task AuthorizeReadAsync_FlagOff_AuthorizesWithoutQuerying()
    {
        var (sutProvider, userId, cipherId) = Setup(enabled: false);

        var access = await sutProvider.Sut.AuthorizeReadAsync(userId, new Cipher { Id = cipherId });

        Assert.NotNull(access);
        Assert.True(access.Authorizes(cipherId));
        // The flag-off path must cost nothing: no rule resolve, no lease read.
        await sutProvider.GetDependency<IGoverningRuleResolver>()
            .DidNotReceiveWithAnyArgs().ResolveAsync(default, default, default!);
        await sutProvider.GetDependency<IAccessLeaseRepository>()
            .DidNotReceiveWithAnyArgs().GetActiveByRequesterIdCipherIdAsync(default, default, default);
    }

    [Fact]
    public async Task AuthorizeReadAsync_NotGated_AuthorizesWithoutReadingLeases()
    {
        var (sutProvider, userId, cipherId) = Setup();
        NotGated(sutProvider, userId, cipherId);

        var access = await sutProvider.Sut.AuthorizeReadAsync(userId, new Cipher { Id = cipherId });

        Assert.NotNull(access);
        Assert.True(access.Authorizes(cipherId));
        // Resolving first means the common case — a cipher no rule governs — never pays for a lease query.
        await sutProvider.GetDependency<IAccessLeaseRepository>()
            .DidNotReceiveWithAnyArgs().GetActiveByRequesterIdCipherIdAsync(default, default, default);
    }

    [Fact]
    public async Task AuthorizeReadAsync_GatedNoLease_ReturnsNull()
    {
        var (sutProvider, userId, cipherId) = Setup();
        Gated(sutProvider, userId, cipherId);

        Assert.Null(await sutProvider.Sut.AuthorizeReadAsync(userId, new Cipher { Id = cipherId }));
    }

    [Fact]
    public async Task AuthorizeReadAsync_GatedWithActiveLease_Authorizes()
    {
        var (sutProvider, userId, cipherId) = Setup();
        Gated(sutProvider, userId, cipherId);
        HasActiveLease(sutProvider, userId, cipherId);

        var access = await sutProvider.Sut.AuthorizeReadAsync(userId, new Cipher { Id = cipherId });

        Assert.NotNull(access);
        Assert.True(access.Authorizes(cipherId));
    }

    [Fact]
    public async Task AuthorizeReadAsync_ReadsLeaseValidityAtTheTimeProvidersNow()
    {
        var (sutProvider, userId, cipherId) = Setup();
        Gated(sutProvider, userId, cipherId);
        HasActiveLease(sutProvider, userId, cipherId);

        await sutProvider.Sut.AuthorizeReadAsync(userId, new Cipher { Id = cipherId });

        // Lease expiry is evaluated against TimeProvider, not DateTime.UtcNow, so an expired lease cannot
        // be kept alive by a stale clock read.
        await sutProvider.GetDependency<IAccessLeaseRepository>().Received(1)
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, _now);
    }

    // --- AuthorizeReadManyAsync (supplied collections) ---------------------------------------------

    [Fact]
    public async Task AuthorizeReadManyAsync_FlagOff_AuthorizesEverything()
    {
        var (sutProvider, userId, gatedCipherId) = Setup(enabled: false);
        var leasingCollectionId = Guid.NewGuid();

        var access = await sutProvider.Sut.AuthorizeReadManyAsync(
            userId,
            [new Cipher { Id = gatedCipherId }],
            [LeasingCollection(leasingCollectionId)],
            Group(new CollectionCipher { CipherId = gatedCipherId, CollectionId = leasingCollectionId }));

        Assert.True(access.Authorizes(gatedCipherId));
    }

    [Fact]
    public async Task AuthorizeReadManyAsync_AuthorizesNonGatedOnly()
    {
        var (sutProvider, userId, gatedCipherId) = Setup();
        var leasingCollectionId = Guid.NewGuid();
        var plainCipherId = Guid.NewGuid();

        var access = await sutProvider.Sut.AuthorizeReadManyAsync(
            userId,
            [new Cipher { Id = gatedCipherId }, new Cipher { Id = plainCipherId }],
            [LeasingCollection(leasingCollectionId)],
            Group(new CollectionCipher { CipherId = gatedCipherId, CollectionId = leasingCollectionId }));

        Assert.False(access.Authorizes(gatedCipherId));
        Assert.True(access.Authorizes(plainCipherId));
    }

    [Fact]
    public async Task AuthorizeReadManyAsync_GatedWithActiveLease_StillWithheld()
    {
        var (sutProvider, userId, gatedCipherId) = Setup();
        var leasingCollectionId = Guid.NewGuid();
        Gated(sutProvider, userId, gatedCipherId);
        HasActiveLease(sutProvider, userId, gatedCipherId);

        var access = await sutProvider.Sut.AuthorizeReadManyAsync(
            userId,
            [new Cipher { Id = gatedCipherId }],
            [LeasingCollection(leasingCollectionId)],
            Group(new CollectionCipher { CipherId = gatedCipherId, CollectionId = leasingCollectionId }));

        // A bulk read is not the act of using a credential. Secrets are only ever released through the
        // single-cipher decision, so a held lease does not widen a sync or a list.
        Assert.False(access.Authorizes(gatedCipherId));
    }

    [Fact]
    public async Task AuthorizeReadManyAsync_AlsoReachableThroughPlainCollection_NotGated()
    {
        var (sutProvider, userId, cipherId) = Setup();
        var leasingCollectionId = Guid.NewGuid();
        var plainCollectionId = Guid.NewGuid();

        var access = await sutProvider.Sut.AuthorizeReadManyAsync(
            userId,
            [new Cipher { Id = cipherId }],
            [LeasingCollection(leasingCollectionId), PlainCollection(plainCollectionId)],
            Group(
                new CollectionCipher { CipherId = cipherId, CollectionId = leasingCollectionId },
                new CollectionCipher { CipherId = cipherId, CollectionId = plainCollectionId }));

        // The caller can already read it in full by the ungoverned path, so withholding it here would only
        // hide a credential leasing does not protect.
        Assert.True(access.Authorizes(cipherId));
    }

    [Fact]
    public async Task AuthorizeReadManyAsync_NoCollectionsLoaded_AuthorizesEverything()
    {
        var (sutProvider, userId, cipherId) = Setup();

        // Null means "not loaded, because the caller has no organizations" — equivalent to empty.
        var access = await sutProvider.Sut.AuthorizeReadManyAsync(
            userId, [new Cipher { Id = cipherId }], null, null);

        Assert.True(access.Authorizes(cipherId));
    }

    [Fact]
    public async Task AuthorizeReadManyAsync_UserOwnedCipherWithNoMapping_NotGated()
    {
        var (sutProvider, userId, userOwnedCipherId) = Setup();
        var leasingCollectionId = Guid.NewGuid();

        // The caller has a leasing collection, but this cipher is reachable through no collection at all.
        var access = await sutProvider.Sut.AuthorizeReadManyAsync(
            userId,
            [new Cipher { Id = userOwnedCipherId }],
            [LeasingCollection(leasingCollectionId)],
            Group());

        Assert.True(access.Authorizes(userOwnedCipherId));
    }

    // --- AuthorizeReadManyAsync (self-loading) ----------------------------------------------------

    [Fact]
    public async Task AuthorizeReadManyAsync_SelfLoading_FlagOff_LoadsNothing()
    {
        var (sutProvider, userId, cipherId) = Setup(enabled: false);

        var access = await sutProvider.Sut.AuthorizeReadManyAsync(userId, [new Cipher { Id = cipherId }]);

        Assert.True(access.Authorizes(cipherId));
        // The whole point of this overload's contract: flag off stays query-free.
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs().GetManyByUserIdAsync(default);
        await sutProvider.GetDependency<ICollectionCipherRepository>()
            .DidNotReceiveWithAnyArgs().GetManyByUserIdAsync(default);
    }

    [Fact]
    public async Task AuthorizeReadManyAsync_SelfLoading_LoadsOnceAndWithholdsGated()
    {
        var (sutProvider, userId, gatedCipherId) = Setup();
        var leasingCollectionId = Guid.NewGuid();
        var plainCipherId = Guid.NewGuid();
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(userId)
            .Returns([LeasingCollection(leasingCollectionId)]);
        sutProvider.GetDependency<ICollectionCipherRepository>().GetManyByUserIdAsync(userId)
            .Returns([new CollectionCipher { CipherId = gatedCipherId, CollectionId = leasingCollectionId }]);

        var access = await sutProvider.Sut.AuthorizeReadManyAsync(
            userId, [new Cipher { Id = gatedCipherId }, new Cipher { Id = plainCipherId }]);

        Assert.False(access.Authorizes(gatedCipherId));
        Assert.True(access.Authorizes(plainCipherId));
        await sutProvider.GetDependency<ICollectionRepository>().Received(1).GetManyByUserIdAsync(userId);
        await sutProvider.GetDependency<ICollectionCipherRepository>().Received(1).GetManyByUserIdAsync(userId);
    }

    // --- Unrestricted -----------------------------------------------------------------------------

    [Fact]
    public void Unrestricted_AuthorizesAnyCipher()
    {
        var (sutProvider, _, _) = Setup();

        var access = sutProvider.Sut.Unrestricted();

        Assert.True(access.Authorizes(Guid.NewGuid()));
    }

    // --- helpers ----------------------------------------------------------------------------------

    private static (SutProvider<CipherLeaseGate> SutProvider, Guid UserId, Guid CipherId) Setup(
        bool enabled = true)
    {
        var sutProvider = new SutProvider<CipherLeaseGate>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(Core.FeatureFlagKeys.Pam).Returns(enabled);
        sutProvider.GetDependency<ICurrentContext>().IpAddress.Returns("198.51.100.7");
        return (sutProvider, Guid.NewGuid(), Guid.NewGuid());
    }

    private static void Gated(SutProvider<CipherLeaseGate> sutProvider, Guid userId, Guid cipherId) =>
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>())
            .Returns(new GoverningRule(Guid.NewGuid(), Guid.NewGuid(), RequiresHumanApproval: false,
                Array.Empty<AccessCondition>()));

    private static void NotGated(SutProvider<CipherLeaseGate> sutProvider, Guid userId, Guid cipherId) =>
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>())
            .Returns((GoverningRule?)null);

    private static void HasActiveLease(SutProvider<CipherLeaseGate> sutProvider, Guid userId, Guid cipherId) =>
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, Arg.Any<DateTime>())
            .Returns(new AccessLease { CipherId = cipherId });

    private static CollectionDetails LeasingCollection(Guid id) =>
        new() { Id = id, AccessRuleId = Guid.NewGuid() };

    private static CollectionDetails PlainCollection(Guid id) => new() { Id = id, AccessRuleId = null };

    private static IDictionary<Guid, IGrouping<Guid, CollectionCipher>> Group(
        params CollectionCipher[] collectionCiphers) =>
        collectionCiphers.GroupBy(cc => cc.CipherId).ToDictionary(g => g.Key);
}
