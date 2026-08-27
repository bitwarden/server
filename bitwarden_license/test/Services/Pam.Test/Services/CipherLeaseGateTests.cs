using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
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
/// The read, write-return and mutation decisions, asserted through the interface only. The structural "which ciphers are
/// gated" rule is exercised via <see cref="CipherLeaseGate.AuthorizeReadManyAsync(Guid, IEnumerable{Cipher}, IEnumerable{CollectionDetails}, IDictionary{Guid, IGrouping{Guid, CollectionCipher}})" />
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

    [Fact]
    public async Task AuthorizeReadManyAsync_GovernedOnlyByDisabledRule_NotGated()
    {
        var (sutProvider, userId, cipherId) = Setup();
        var disabledRuleCollectionId = Guid.NewGuid();

        var access = await sutProvider.Sut.AuthorizeReadManyAsync(
            userId,
            [new Cipher { Id = cipherId }],
            [DisabledRuleCollection(disabledRuleCollectionId)],
            Group(new CollectionCipher { CipherId = cipherId, CollectionId = disabledRuleCollectionId }));

        // A switched-off rule gates nothing, which is the reading the single-cipher path already took: the
        // resolver drops a disabled rule, so gating here withheld the credential while offering no way to
        // request it — no data and no prompt either (PM-42274).
        Assert.True(access.Authorizes(cipherId));
    }

    [Fact]
    public async Task AuthorizeReadManyAsync_AlsoReachableThroughDisabledRuleCollection_NotGated()
    {
        var (sutProvider, userId, cipherId) = Setup();
        var leasingCollectionId = Guid.NewGuid();
        var disabledRuleCollectionId = Guid.NewGuid();

        var access = await sutProvider.Sut.AuthorizeReadManyAsync(
            userId,
            [new Cipher { Id = cipherId }],
            [LeasingCollection(leasingCollectionId), DisabledRuleCollection(disabledRuleCollectionId)],
            Group(
                new CollectionCipher { CipherId = cipherId, CollectionId = leasingCollectionId },
                new CollectionCipher { CipherId = cipherId, CollectionId = disabledRuleCollectionId }));

        // The disabled path is an escape for the same reason a plain collection is: it gates nothing, so the
        // caller can already read the cipher in full through it.
        Assert.True(access.Authorizes(cipherId));
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

    [Fact]
    public async Task AuthorizeReadManyAsync_SelfLoading_GovernedOnlyByDisabledRule_NotGated()
    {
        var (sutProvider, userId, cipherId) = Setup();
        var disabledRuleCollectionId = Guid.NewGuid();
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(userId)
            .Returns([DisabledRuleCollection(disabledRuleCollectionId)]);
        sutProvider.GetDependency<ICollectionCipherRepository>().GetManyByUserIdAsync(userId)
            .Returns([new CollectionCipher { CipherId = cipherId, CollectionId = disabledRuleCollectionId }]);

        var access = await sutProvider.Sut.AuthorizeReadManyAsync(userId, [new Cipher { Id = cipherId }]);

        Assert.True(access.Authorizes(cipherId));
    }

    // --- AuthorizeWriteReturnAsync -----------------------------------------------------------------

    [Fact]
    public async Task AuthorizeWriteReturnAsync_FlagOff_AuthorizesWithoutQuerying()
    {
        var (sutProvider, userId, cipherId) = Setup(enabled: false);

        var access = await sutProvider.Sut.AuthorizeWriteReturnAsync(userId, new Cipher { Id = cipherId });

        Assert.NotNull(access);
        Assert.True(access.Authorizes(cipherId));
        await sutProvider.GetDependency<IGoverningRuleResolver>()
            .DidNotReceiveWithAnyArgs().ResolveAsync(default, default, default!);
    }

    [Fact]
    public async Task AuthorizeWriteReturnAsync_NotGated_Authorizes()
    {
        var (sutProvider, userId, cipherId) = Setup();
        NotGated(sutProvider, userId, cipherId);

        var access = await sutProvider.Sut.AuthorizeWriteReturnAsync(userId, new Cipher { Id = cipherId });

        Assert.NotNull(access);
        Assert.True(access.Authorizes(cipherId));
    }

    /// <remarks>
    /// The decision this method exists for. A lease widens the single <em>read</em>, and widened the echo of a
    /// mutation with it until the write-return was split out — landing the full secret in durable client
    /// state, where it outlived the lease.
    /// </remarks>
    [Fact]
    public async Task AuthorizeWriteReturnAsync_GatedWithActiveLease_Withholds()
    {
        var (sutProvider, userId, cipherId) = Setup();
        Gated(sutProvider, userId, cipherId);
        HasActiveLease(sutProvider, userId, cipherId);

        var access = await sutProvider.Sut.AuthorizeWriteReturnAsync(userId, new Cipher { Id = cipherId });

        Assert.Null(access);
    }

    [Fact]
    public async Task AuthorizeWriteReturnAsync_GatedNoLease_Withholds()
    {
        var (sutProvider, userId, cipherId) = Setup();
        Gated(sutProvider, userId, cipherId);

        var access = await sutProvider.Sut.AuthorizeWriteReturnAsync(userId, new Cipher { Id = cipherId });

        Assert.Null(access);
    }

    [Fact]
    public async Task AuthorizeWriteReturnAsync_Gated_DoesNotReadLeases()
    {
        var (sutProvider, userId, cipherId) = Setup();
        Gated(sutProvider, userId, cipherId);

        await sutProvider.Sut.AuthorizeWriteReturnAsync(userId, new Cipher { Id = cipherId });

        // Lease state cannot change the answer, so paying for the query would be waste.
        await sutProvider.GetDependency<IAccessLeaseRepository>()
            .DidNotReceiveWithAnyArgs().GetActiveByRequesterIdCipherIdAsync(default, default, default);
    }

    // --- AuthorizeAdminWriteReturnAsync ------------------------------------------------------------

    [Fact]
    public async Task AuthorizeAdminWriteReturnAsync_FlagOff_AuthorizesWithoutQuerying()
    {
        var (sutProvider, userId, cipherId) = Setup(enabled: false);

        var access = await sutProvider.Sut.AuthorizeAdminWriteReturnAsync(
            userId, Guid.NewGuid(), new Cipher { Id = cipherId });

        Assert.NotNull(access);
        Assert.True(access.Authorizes(cipherId));
        await sutProvider.GetDependency<IAccessRuleRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdAsync(default);
    }

    [Fact]
    public async Task AuthorizeAdminWriteReturnAsync_NotGated_Authorizes()
    {
        var (sutProvider, userId, cipherId) = Setup();
        var organizationId = Guid.NewGuid();
        OrganizationLeasingCollection(sutProvider, organizationId, Guid.NewGuid());
        CipherIsInCollections(sutProvider, cipherId, Guid.NewGuid());

        var access = await sutProvider.Sut.AuthorizeAdminWriteReturnAsync(
            userId, organizationId, new Cipher { Id = cipherId });

        Assert.NotNull(access);
        Assert.True(access.Authorizes(cipherId));
    }

    [Fact]
    public async Task AuthorizeAdminWriteReturnAsync_GatedWithActiveLease_Withholds()
    {
        var (sutProvider, userId, cipherId) = Setup();
        var organizationId = Guid.NewGuid();
        var collectionId = Guid.NewGuid();
        OrganizationLeasingCollection(sutProvider, organizationId, collectionId);
        CipherIsInCollections(sutProvider, cipherId, collectionId);
        HasActiveLease(sutProvider, userId, cipherId);

        var access = await sutProvider.Sut.AuthorizeAdminWriteReturnAsync(
            userId, organizationId, new Cipher { Id = cipherId });

        Assert.Null(access);
        await sutProvider.GetDependency<IAccessLeaseRepository>()
            .DidNotReceiveWithAnyArgs().GetActiveByRequesterIdCipherIdAsync(default, default, default);
    }

    [Fact]
    public async Task AuthorizeAdminWriteReturnAsync_DisabledRule_Authorizes()
    {
        var (sutProvider, userId, cipherId) = Setup();
        var organizationId = Guid.NewGuid();
        var collectionId = Guid.NewGuid();
        OrganizationLeasingCollection(sutProvider, organizationId, collectionId, ruleEnabled: false);
        CipherIsInCollections(sutProvider, cipherId, collectionId);

        var access = await sutProvider.Sut.AuthorizeAdminWriteReturnAsync(
            userId, organizationId, new Cipher { Id = cipherId });

        Assert.NotNull(access);
        Assert.True(access.Authorizes(cipherId));
    }

    // --- EnsureCanMutateAsync ---------------------------------------------------------------------

    [Fact]
    public async Task EnsureCanMutateAsync_FlagOff_AuthorizesWithoutQuerying()
    {
        var (sutProvider, userId, cipherId) = Setup(enabled: false);

        var access = await sutProvider.Sut.EnsureCanMutateAsync(userId, new Cipher { Id = cipherId });

        Assert.True(access.Authorizes(cipherId));
        await sutProvider.GetDependency<IGoverningRuleResolver>()
            .DidNotReceiveWithAnyArgs().ResolveAsync(default, default, default!);
        await sutProvider.GetDependency<IAccessLeaseRepository>()
            .DidNotReceiveWithAnyArgs().GetActiveByRequesterIdCipherIdAsync(default, default, default);
    }

    [Fact]
    public async Task EnsureCanMutateAsync_NotGated_AuthorizesWithoutReadingLeases()
    {
        var (sutProvider, userId, cipherId) = Setup();
        NotGated(sutProvider, userId, cipherId);

        var access = await sutProvider.Sut.EnsureCanMutateAsync(userId, new Cipher { Id = cipherId });

        Assert.True(access.Authorizes(cipherId));
        await sutProvider.GetDependency<IAccessLeaseRepository>()
            .DidNotReceiveWithAnyArgs().GetActiveByRequesterIdCipherIdAsync(default, default, default);
    }

    [Fact]
    public async Task EnsureCanMutateAsync_GatedNoLease_ThrowsNotFound()
    {
        var (sutProvider, userId, cipherId) = Setup();
        Gated(sutProvider, userId, cipherId);

        // NotFound, not forbidden: a write attempt must not confirm that a credential the caller cannot
        // reach exists.
        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.EnsureCanMutateAsync(userId, new Cipher { Id = cipherId }));
    }

    [Fact]
    public async Task EnsureCanMutateAsync_GatedWithActiveLease_Authorizes()
    {
        var (sutProvider, userId, cipherId) = Setup();
        Gated(sutProvider, userId, cipherId);
        HasActiveLease(sutProvider, userId, cipherId);

        var access = await sutProvider.Sut.EnsureCanMutateAsync(userId, new Cipher { Id = cipherId });

        // The lease exists to grant this access; a write emits no secret, so holding one permits the edit.
        Assert.True(access.Authorizes(cipherId));
    }

    [Fact]
    public async Task EnsureCanMutateAsync_ReadsLeaseValidityAtTheTimeProvidersNow()
    {
        var (sutProvider, userId, cipherId) = Setup();
        Gated(sutProvider, userId, cipherId);
        HasActiveLease(sutProvider, userId, cipherId);

        await sutProvider.Sut.EnsureCanMutateAsync(userId, new Cipher { Id = cipherId });

        await sutProvider.GetDependency<IAccessLeaseRepository>().Received(1)
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, _now);
    }

    // --- EnsureCanMutateManyAsync -----------------------------------------------------------------

    [Fact]
    public async Task EnsureCanMutateManyAsync_FlagOff_AuthorizesWithoutQuerying()
    {
        var (sutProvider, userId, cipherId) = Setup(enabled: false);

        var access = await sutProvider.Sut.EnsureCanMutateManyAsync(userId, [new Cipher { Id = cipherId }]);

        Assert.True(access.Authorizes(cipherId));
        await sutProvider.GetDependency<IAccessLeaseRepository>()
            .DidNotReceiveWithAnyArgs().GetManyActiveByRequesterIdAsync(default, default);
        await sutProvider.GetDependency<IGoverningRuleResolver>()
            .DidNotReceiveWithAnyArgs().ResolveAsync(default, default, default!);
    }

    [Fact]
    public async Task EnsureCanMutateManyAsync_NoCiphers_AuthorizesNothingAndQueriesNothing()
    {
        var (sutProvider, userId, cipherId) = Setup();

        var access = await sutProvider.Sut.EnsureCanMutateManyAsync(userId, []);

        Assert.False(access.Authorizes(cipherId));
        await sutProvider.GetDependency<IAccessLeaseRepository>()
            .DidNotReceiveWithAnyArgs().GetManyActiveByRequesterIdAsync(default, default);
    }

    [Fact]
    public async Task EnsureCanMutateManyAsync_NoneGated_AuthorizesEveryCipher()
    {
        var (sutProvider, userId, firstCipherId) = Setup();
        var secondCipherId = Guid.NewGuid();
        HasNoActiveLeases(sutProvider, userId);
        NotGated(sutProvider, userId, firstCipherId);
        NotGated(sutProvider, userId, secondCipherId);

        var access = await sutProvider.Sut.EnsureCanMutateManyAsync(
            userId, [new Cipher { Id = firstCipherId }, new Cipher { Id = secondCipherId }]);

        Assert.True(access.Authorizes(firstCipherId));
        Assert.True(access.Authorizes(secondCipherId));
    }

    [Fact]
    public async Task EnsureCanMutateManyAsync_OneGatedNoLease_ThrowsNotFoundForTheWholeBatch()
    {
        var (sutProvider, userId, gatedCipherId) = Setup();
        var plainCipherId = Guid.NewGuid();
        HasNoActiveLeases(sutProvider, userId);
        NotGated(sutProvider, userId, plainCipherId);
        Gated(sutProvider, userId, gatedCipherId);

        // All-or-nothing: a half-applied bulk delete would leave the caller unable to tell what happened.
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.EnsureCanMutateManyAsync(
            userId, [new Cipher { Id = plainCipherId }, new Cipher { Id = gatedCipherId }]));
    }

    [Fact]
    public async Task EnsureCanMutateManyAsync_GatedWithActiveLease_Authorizes()
    {
        var (sutProvider, userId, gatedCipherId) = Setup();
        Gated(sutProvider, userId, gatedCipherId);
        HasActiveLeasesFor(sutProvider, userId, gatedCipherId);

        var access = await sutProvider.Sut.EnsureCanMutateManyAsync(
            userId, [new Cipher { Id = gatedCipherId }]);

        // Deliberately unlike the bulk *read*, which withholds a gated cipher whatever the lease state.
        Assert.True(access.Authorizes(gatedCipherId));
    }

    [Fact]
    public async Task EnsureCanMutateManyAsync_LeasedCipher_SkipsTheRuleResolve()
    {
        var (sutProvider, userId, cipherId) = Setup();
        HasActiveLeasesFor(sutProvider, userId, cipherId);

        await sutProvider.Sut.EnsureCanMutateManyAsync(userId, [new Cipher { Id = cipherId }]);

        // A lease authorizes the mutation whatever rule governs the cipher, so resolving would be wasted work.
        await sutProvider.GetDependency<IGoverningRuleResolver>()
            .DidNotReceiveWithAnyArgs().ResolveAsync(default, default, default!);
    }

    [Fact]
    public async Task EnsureCanMutateManyAsync_ReadsLeasesOnceForTheWholeBatch()
    {
        var (sutProvider, userId, firstCipherId) = Setup();
        var secondCipherId = Guid.NewGuid();
        HasNoActiveLeases(sutProvider, userId);
        NotGated(sutProvider, userId, firstCipherId);
        NotGated(sutProvider, userId, secondCipherId);

        await sutProvider.Sut.EnsureCanMutateManyAsync(
            userId, [new Cipher { Id = firstCipherId }, new Cipher { Id = secondCipherId }]);

        // Per-cipher lease reads would make a bulk mutation cost O(n) lease queries on top of the resolves.
        await sutProvider.GetDependency<IAccessLeaseRepository>().Received(1)
            .GetManyActiveByRequesterIdAsync(userId, _now);
    }

    [Fact]
    public async Task EnsureCanMutateManyAsync_RepeatedCipherId_ResolvesItOnce()
    {
        var (sutProvider, userId, cipherId) = Setup();
        HasNoActiveLeases(sutProvider, userId);
        NotGated(sutProvider, userId, cipherId);

        await sutProvider.Sut.EnsureCanMutateManyAsync(
            userId, [new Cipher { Id = cipherId }, new Cipher { Id = cipherId }]);

        // MoveManyAsync forwards request ids straight through, so duplicates reach the gate.
        await sutProvider.GetDependency<IGoverningRuleResolver>().Received(1)
            .ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>());
    }

    // --- UnrestrictedForWholeVaultExport ----------------------------------------------------------

    [Fact]
    public void UnrestrictedForWholeVaultExport_AuthorizesAnyCipher()
    {
        var (sutProvider, _, _) = Setup();

        var access = sutProvider.Sut.UnrestrictedForWholeVaultExport();

        Assert.True(access.Authorizes(Guid.NewGuid()));
    }

    // --- AuthorizeAdminReadAsync -------------------------------------------------------------------

    [Fact]
    public async Task AuthorizeAdminReadAsync_FlagOff_AuthorizesWithoutQuerying()
    {
        var (sutProvider, userId, cipherId) = Setup(enabled: false);

        var access = await sutProvider.Sut.AuthorizeAdminReadAsync(userId, Guid.NewGuid(), new Cipher { Id = cipherId });

        Assert.NotNull(access);
        Assert.True(access.Authorizes(cipherId));
        await sutProvider.GetDependency<IAccessRuleRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdAsync(default);
    }

    /// <remarks>
    /// The decision an administrator's assignments must not reach. The caller is assigned to nothing, which
    /// is what the member paths read as "not gated" — this must still withhold.
    /// </remarks>
    [Fact]
    public async Task AuthorizeAdminReadAsync_GatedAndCallerAssignedToNothing_Withholds()
    {
        var (sutProvider, userId, cipherId) = Setup();
        var organizationId = Guid.NewGuid();
        var collectionId = Guid.NewGuid();
        OrganizationLeasingCollection(sutProvider, organizationId, collectionId);
        CipherIsInCollections(sutProvider, cipherId, collectionId);
        // The member paths would resolve nothing for this caller and let it through.
        NotGated(sutProvider, userId, cipherId);

        var access = await sutProvider.Sut.AuthorizeAdminReadAsync(userId, organizationId, new Cipher { Id = cipherId });

        Assert.Null(access);
    }

    [Fact]
    public async Task AuthorizeAdminReadAsync_GatedWithActiveLease_Authorizes()
    {
        var (sutProvider, userId, cipherId) = Setup();
        var organizationId = Guid.NewGuid();
        var collectionId = Guid.NewGuid();
        OrganizationLeasingCollection(sutProvider, organizationId, collectionId);
        CipherIsInCollections(sutProvider, cipherId, collectionId);
        HasActiveLease(sutProvider, userId, cipherId);

        var access = await sutProvider.Sut.AuthorizeAdminReadAsync(userId, organizationId, new Cipher { Id = cipherId });

        Assert.NotNull(access);
        Assert.True(access.Authorizes(cipherId));
    }

    [Fact]
    public async Task AuthorizeAdminReadAsync_DisabledRule_Authorizes()
    {
        var (sutProvider, userId, cipherId) = Setup();
        var organizationId = Guid.NewGuid();
        var collectionId = Guid.NewGuid();
        OrganizationLeasingCollection(sutProvider, organizationId, collectionId, ruleEnabled: false);
        CipherIsInCollections(sutProvider, cipherId, collectionId);

        var access = await sutProvider.Sut.AuthorizeAdminReadAsync(userId, organizationId, new Cipher { Id = cipherId });

        Assert.NotNull(access);
        Assert.True(access.Authorizes(cipherId));
    }

    /// <remarks>
    /// An unassigned organization cipher sits in no collection, so there is no leasing-enabled collection
    /// to reach it through and nothing to gate. The "/admin" endpoints reach these — <c>Admin</c> and
    /// <c>Owner</c> pass <c>CanAccessUnassignedCiphersAsync</c> — so the administrative decision has to
    /// answer for them.
    /// </remarks>
    [Fact]
    public async Task AuthorizeAdminReadAsync_UnassignedCipher_Authorizes()
    {
        var (sutProvider, userId, cipherId) = Setup();
        var organizationId = Guid.NewGuid();
        OrganizationLeasingCollection(sutProvider, organizationId, Guid.NewGuid());
        CipherIsInCollections(sutProvider, cipherId);

        var access = await sutProvider.Sut.AuthorizeAdminReadAsync(userId, organizationId, new Cipher { Id = cipherId });

        Assert.NotNull(access);
        Assert.True(access.Authorizes(cipherId));
    }

    [Fact]
    public async Task AuthorizeAdminReadAsync_AlsoInAPlainCollection_Authorizes()
    {
        var (sutProvider, userId, cipherId) = Setup();
        var organizationId = Guid.NewGuid();
        var leasingCollectionId = Guid.NewGuid();
        OrganizationLeasingCollection(sutProvider, organizationId, leasingCollectionId);
        CipherIsInCollections(sutProvider, cipherId, leasingCollectionId, Guid.NewGuid());

        var access = await sutProvider.Sut.AuthorizeAdminReadAsync(userId, organizationId, new Cipher { Id = cipherId });

        Assert.NotNull(access);
        Assert.True(access.Authorizes(cipherId));
    }

    // --- AuthorizeAdminReadManyAsync ---------------------------------------------------------------

    /// <remarks>
    /// The bulk decision strips every gated cipher whatever the lease state, matching the member bulk rule:
    /// secrets are released one cipher at a time.
    /// </remarks>
    [Fact]
    public async Task AuthorizeAdminReadManyAsync_StripsGatedEvenWithAnActiveLease()
    {
        var (sutProvider, userId, _) = Setup();
        var organizationId = Guid.NewGuid();
        var leasingCollectionId = Guid.NewGuid();
        var plainCollectionId = Guid.NewGuid();
        var gatedCipherId = Guid.NewGuid();
        var plainCipherId = Guid.NewGuid();

        OrganizationLeasingCollection(sutProvider, organizationId, leasingCollectionId);
        sutProvider.GetDependency<ICollectionCipherRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(new List<CollectionCipher>
            {
                new() { CipherId = gatedCipherId, CollectionId = leasingCollectionId },
                new() { CipherId = plainCipherId, CollectionId = plainCollectionId },
            });
        HasActiveLease(sutProvider, userId, gatedCipherId);

        var access = await sutProvider.Sut.AuthorizeAdminReadManyAsync(userId, organizationId,
            [new Cipher { Id = gatedCipherId }, new Cipher { Id = plainCipherId }]);

        Assert.False(access.Authorizes(gatedCipherId));
        Assert.True(access.Authorizes(plainCipherId));
    }

    [Fact]
    public async Task AuthorizeAdminReadManyAsync_NoEnabledRules_AuthorizesEverything()
    {
        var (sutProvider, userId, _) = Setup();
        var organizationId = Guid.NewGuid();
        var cipherId = Guid.NewGuid();
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(new List<AccessRule>());

        var access = await sutProvider.Sut.AuthorizeAdminReadManyAsync(userId, organizationId,
            [new Cipher { Id = cipherId }]);

        Assert.True(access.Authorizes(cipherId));
        await sutProvider.GetDependency<ICollectionCipherRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdAsync(default);
    }

    // --- helpers ----------------------------------------------------------------------------------

    /// <summary>
    /// Points the organization-scoped reads at a single collection governed by a rule, so the gate resolves
    /// it as leasing-enabled (or not, when <paramref name="ruleEnabled" /> is false).
    /// </summary>
    private static void OrganizationLeasingCollection(SutProvider<CipherLeaseGate> sutProvider,
        Guid organizationId, Guid collectionId, bool ruleEnabled = true)
    {
        var ruleId = Guid.NewGuid();
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(new List<AccessRule> { new() { Id = ruleId, Enabled = ruleEnabled } });
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(new List<Collection> { new() { Id = collectionId, AccessRuleId = ruleId } });
    }

    private static void CipherIsInCollections(SutProvider<CipherLeaseGate> sutProvider, Guid cipherId,
        params Guid[] collectionIds) =>
        sutProvider.GetDependency<ICollectionCipherRepository>()
            .GetCollectionIdsByCipherIdAsync(cipherId)
            .Returns(collectionIds.ToList());

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

    private static void HasActiveLeasesFor(SutProvider<CipherLeaseGate> sutProvider, Guid userId,
        params Guid[] cipherIds) =>
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetManyActiveByRequesterIdAsync(userId, Arg.Any<DateTime>())
            .Returns(cipherIds.Select(id => new AccessLease { CipherId = id }).ToList());

    private static void HasNoActiveLeases(SutProvider<CipherLeaseGate> sutProvider, Guid userId) =>
        HasActiveLeasesFor(sutProvider, userId);

    private static CollectionDetails LeasingCollection(Guid id) =>
        new() { Id = id, AccessRuleId = Guid.NewGuid(), HasEnabledAccessRule = true };

    /// <summary>
    /// A collection associated with a rule the admin has switched off. The association is still recorded, so
    /// this is exactly the shape that used to gate on the bare <c>AccessRuleId</c>.
    /// </summary>
    private static CollectionDetails DisabledRuleCollection(Guid id) =>
        new() { Id = id, AccessRuleId = Guid.NewGuid(), HasEnabledAccessRule = false };

    private static CollectionDetails PlainCollection(Guid id) => new() { Id = id, AccessRuleId = null };

    private static IDictionary<Guid, IGrouping<Guid, CollectionCipher>> Group(
        params CollectionCipher[] collectionCiphers) =>
        collectionCiphers.GroupBy(cc => cc.CipherId).ToDictionary(g => g.Key);
}
