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

/// <summary>The gated-cipher rule is tested through the bulk read to stay on the public interface.</summary>
public class CipherLeaseGateTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

    private static readonly Guid _organizationId = Guid.NewGuid();

    [Fact]
    public async Task AuthorizeReadAsync_FlagOff_AuthorizesWithoutQuerying()
    {
        var (sutProvider, userId, cipherId) = Setup(enabled: false);

        var access = await sutProvider.Sut.AuthorizeReadAsync(userId, new Cipher { Id = cipherId, OrganizationId = _organizationId });

        Assert.NotNull(access);
        Assert.True(access.Authorizes(cipherId));
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

        var access = await sutProvider.Sut.AuthorizeReadAsync(userId, new Cipher { Id = cipherId, OrganizationId = _organizationId });

        Assert.NotNull(access);
        Assert.True(access.Authorizes(cipherId));
        // Resolving first spares the common case, an ungoverned cipher, a lease query.
        await sutProvider.GetDependency<IAccessLeaseRepository>()
            .DidNotReceiveWithAnyArgs().GetActiveByRequesterIdCipherIdAsync(default, default, default);
    }

    [Fact]
    public async Task AuthorizeReadAsync_GatedNoLease_ReturnsNull()
    {
        var (sutProvider, userId, cipherId) = Setup();
        Gated(sutProvider, userId, cipherId);

        Assert.Null(await sutProvider.Sut.AuthorizeReadAsync(userId, new Cipher { Id = cipherId, OrganizationId = _organizationId }));
    }

    [Fact]
    public async Task AuthorizeReadAsync_GatedWithActiveLease_Authorizes()
    {
        var (sutProvider, userId, cipherId) = Setup();
        Gated(sutProvider, userId, cipherId);
        HasActiveLease(sutProvider, userId, cipherId);

        var access = await sutProvider.Sut.AuthorizeReadAsync(userId, new Cipher { Id = cipherId, OrganizationId = _organizationId });

        Assert.NotNull(access);
        Assert.True(access.Authorizes(cipherId));
    }

    [Fact]
    public async Task AuthorizeReadAsync_ReadsLeaseValidityAtTheTimeProvidersNow()
    {
        var (sutProvider, userId, cipherId) = Setup();
        Gated(sutProvider, userId, cipherId);
        HasActiveLease(sutProvider, userId, cipherId);

        await sutProvider.Sut.AuthorizeReadAsync(userId, new Cipher { Id = cipherId, OrganizationId = _organizationId });

        await sutProvider.GetDependency<IAccessLeaseRepository>().Received(1)
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, _now);
    }

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
            [new Cipher { Id = cipherId, OrganizationId = _organizationId }],
            [LeasingCollection(leasingCollectionId), PlainCollection(plainCollectionId)],
            Group(
                new CollectionCipher { CipherId = cipherId, CollectionId = leasingCollectionId },
                new CollectionCipher { CipherId = cipherId, CollectionId = plainCollectionId }));

        Assert.True(access.Authorizes(cipherId));
    }

    [Fact]
    public async Task AuthorizeReadManyAsync_NoCollectionsLoaded_AuthorizesEverything()
    {
        var (sutProvider, userId, cipherId) = Setup();

        // Null means not loaded, equivalent to empty.
        var access = await sutProvider.Sut.AuthorizeReadManyAsync(
            userId, [new Cipher { Id = cipherId, OrganizationId = _organizationId }], null, null);

        Assert.True(access.Authorizes(cipherId));
    }

    [Fact]
    public async Task AuthorizeReadManyAsync_UserOwnedCipherWithNoMapping_NotGated()
    {
        var (sutProvider, userId, userOwnedCipherId) = Setup();
        var leasingCollectionId = Guid.NewGuid();

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
            [new Cipher { Id = cipherId, OrganizationId = _organizationId }],
            [DisabledRuleCollection(disabledRuleCollectionId)],
            Group(new CollectionCipher { CipherId = cipherId, CollectionId = disabledRuleCollectionId }));

        // The resolver ignores a disabled rule, so the bulk read does too.
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
            [new Cipher { Id = cipherId, OrganizationId = _organizationId }],
            [LeasingCollection(leasingCollectionId), DisabledRuleCollection(disabledRuleCollectionId)],
            Group(
                new CollectionCipher { CipherId = cipherId, CollectionId = leasingCollectionId },
                new CollectionCipher { CipherId = cipherId, CollectionId = disabledRuleCollectionId }));

        Assert.True(access.Authorizes(cipherId));
    }

    [Fact]
    public async Task AuthorizeReadManyAsync_SelfLoading_FlagOff_LoadsNothing()
    {
        var (sutProvider, userId, cipherId) = Setup(enabled: false);

        var access = await sutProvider.Sut.AuthorizeReadManyAsync(userId, [new Cipher { Id = cipherId, OrganizationId = _organizationId }]);

        Assert.True(access.Authorizes(cipherId));
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

        var access = await sutProvider.Sut.AuthorizeReadManyAsync(userId, [new Cipher { Id = cipherId, OrganizationId = _organizationId }]);

        Assert.True(access.Authorizes(cipherId));
    }

    [Fact]
    public async Task AuthorizeWriteReturnAsync_FlagOff_AuthorizesWithoutQuerying()
    {
        var (sutProvider, userId, cipherId) = Setup(enabled: false);

        var access = await sutProvider.Sut.AuthorizeWriteReturnAsync(userId, new Cipher { Id = cipherId, OrganizationId = _organizationId });

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

        var access = await sutProvider.Sut.AuthorizeWriteReturnAsync(userId, new Cipher { Id = cipherId, OrganizationId = _organizationId });

        Assert.NotNull(access);
        Assert.True(access.Authorizes(cipherId));
    }

    /// <remarks>A lease widens the single read, not the echo of a mutation.</remarks>
    [Fact]
    public async Task AuthorizeWriteReturnAsync_GatedWithActiveLease_Withholds()
    {
        var (sutProvider, userId, cipherId) = Setup();
        Gated(sutProvider, userId, cipherId);
        HasActiveLease(sutProvider, userId, cipherId);

        var access = await sutProvider.Sut.AuthorizeWriteReturnAsync(userId, new Cipher { Id = cipherId, OrganizationId = _organizationId });

        Assert.Null(access);
    }

    [Fact]
    public async Task AuthorizeWriteReturnAsync_GatedNoLease_Withholds()
    {
        var (sutProvider, userId, cipherId) = Setup();
        Gated(sutProvider, userId, cipherId);

        var access = await sutProvider.Sut.AuthorizeWriteReturnAsync(userId, new Cipher { Id = cipherId, OrganizationId = _organizationId });

        Assert.Null(access);
    }

    [Fact]
    public async Task AuthorizeWriteReturnAsync_Gated_DoesNotReadLeases()
    {
        var (sutProvider, userId, cipherId) = Setup();
        Gated(sutProvider, userId, cipherId);

        await sutProvider.Sut.AuthorizeWriteReturnAsync(userId, new Cipher { Id = cipherId, OrganizationId = _organizationId });

        // Lease state cannot change the answer, so the query would be waste.
        await sutProvider.GetDependency<IAccessLeaseRepository>()
            .DidNotReceiveWithAnyArgs().GetActiveByRequesterIdCipherIdAsync(default, default, default);
    }

    [Fact]
    public async Task AuthorizeAdminWriteReturnAsync_FlagOff_AuthorizesWithoutQuerying()
    {
        var (sutProvider, userId, cipherId) = Setup(enabled: false);

        var access = await sutProvider.Sut.AuthorizeAdminWriteReturnAsync(
            userId, Guid.NewGuid(), new Cipher { Id = cipherId, OrganizationId = _organizationId });

        Assert.NotNull(access);
        Assert.True(access.Authorizes(cipherId));
        await sutProvider.GetDependency<IGatingCollectionResolver>().DidNotReceiveWithAnyArgs()
            .GetGatingCollectionIdsAsync(default);
    }

    [Fact]
    public async Task AuthorizeAdminWriteReturnAsync_NotGated_Authorizes()
    {
        var (sutProvider, userId, cipherId) = Setup();
        var organizationId = Guid.NewGuid();
        OrganizationLeasingCollection(sutProvider, organizationId, Guid.NewGuid());
        CipherIsInCollections(sutProvider, cipherId, Guid.NewGuid());

        var access = await sutProvider.Sut.AuthorizeAdminWriteReturnAsync(
            userId, organizationId, new Cipher { Id = cipherId, OrganizationId = _organizationId });

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
            userId, organizationId, new Cipher { Id = cipherId, OrganizationId = _organizationId });

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
            userId, organizationId, new Cipher { Id = cipherId, OrganizationId = _organizationId });

        Assert.NotNull(access);
        Assert.True(access.Authorizes(cipherId));
    }

    [Fact]
    public async Task EnsureCanMutateAsync_FlagOff_AuthorizesWithoutQuerying()
    {
        var (sutProvider, userId, cipherId) = Setup(enabled: false);

        var access = await sutProvider.Sut.EnsureCanMutateAsync(userId, new Cipher { Id = cipherId, OrganizationId = _organizationId });

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

        var access = await sutProvider.Sut.EnsureCanMutateAsync(userId, new Cipher { Id = cipherId, OrganizationId = _organizationId });

        Assert.True(access.Authorizes(cipherId));
        await sutProvider.GetDependency<IAccessLeaseRepository>()
            .DidNotReceiveWithAnyArgs().GetActiveByRequesterIdCipherIdAsync(default, default, default);
    }

    [Fact]
    public async Task EnsureCanMutateAsync_GatedNoLease_ThrowsNotFound()
    {
        var (sutProvider, userId, cipherId) = Setup();
        Gated(sutProvider, userId, cipherId);

        // NotFound, not Forbidden, so a write attempt does not reveal that the credential exists.
        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.EnsureCanMutateAsync(userId, new Cipher { Id = cipherId, OrganizationId = _organizationId }));
    }

    [Fact]
    public async Task EnsureCanMutateAsync_GatedWithActiveLease_Authorizes()
    {
        var (sutProvider, userId, cipherId) = Setup();
        Gated(sutProvider, userId, cipherId);
        HasActiveLease(sutProvider, userId, cipherId);

        var access = await sutProvider.Sut.EnsureCanMutateAsync(userId, new Cipher { Id = cipherId, OrganizationId = _organizationId });

        // A write emits no secret, so holding a lease permits the edit.
        Assert.True(access.Authorizes(cipherId));
    }

    [Fact]
    public async Task EnsureCanMutateAsync_ReadsLeaseValidityAtTheTimeProvidersNow()
    {
        var (sutProvider, userId, cipherId) = Setup();
        Gated(sutProvider, userId, cipherId);
        HasActiveLease(sutProvider, userId, cipherId);

        await sutProvider.Sut.EnsureCanMutateAsync(userId, new Cipher { Id = cipherId, OrganizationId = _organizationId });

        await sutProvider.GetDependency<IAccessLeaseRepository>().Received(1)
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, _now);
    }

    [Fact]
    public async Task EnsureCanMutateManyAsync_FlagOff_AuthorizesWithoutQuerying()
    {
        var (sutProvider, userId, cipherId) = Setup(enabled: false);

        var access = await sutProvider.Sut.EnsureCanMutateManyAsync(userId, [new Cipher { Id = cipherId, OrganizationId = _organizationId }]);

        Assert.True(access.Authorizes(cipherId));
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs().GetManyByUserIdAsync(default);
        await sutProvider.GetDependency<IAccessLeaseRepository>()
            .DidNotReceiveWithAnyArgs().GetManyActiveByRequesterIdAsync(default, default);
    }

    [Fact]
    public async Task EnsureCanMutateManyAsync_NoCiphers_AuthorizesNothingAndQueriesNothing()
    {
        var (sutProvider, userId, cipherId) = Setup();

        var access = await sutProvider.Sut.EnsureCanMutateManyAsync(userId, []);

        Assert.False(access.Authorizes(cipherId));
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs().GetManyByUserIdAsync(default);
        await sutProvider.GetDependency<IAccessLeaseRepository>()
            .DidNotReceiveWithAnyArgs().GetManyActiveByRequesterIdAsync(default, default);
    }

    [Fact]
    public async Task EnsureCanMutateManyAsync_NoneGated_AuthorizesEveryCipherWithoutReadingLeases()
    {
        var (sutProvider, userId, firstCipherId) = Setup();
        var secondCipherId = Guid.NewGuid();
        CallerGates(sutProvider, userId);

        var access = await sutProvider.Sut.EnsureCanMutateManyAsync(
            userId, [new Cipher { Id = firstCipherId }, new Cipher { Id = secondCipherId }]);

        Assert.True(access.Authorizes(firstCipherId));
        Assert.True(access.Authorizes(secondCipherId));
        await sutProvider.GetDependency<IAccessLeaseRepository>()
            .DidNotReceiveWithAnyArgs().GetManyActiveByRequesterIdAsync(default, default);
    }

    [Fact]
    public async Task EnsureCanMutateManyAsync_OneGatedNoLease_ThrowsNotFoundForTheWholeBatch()
    {
        var (sutProvider, userId, gatedCipherId) = Setup();
        var plainCipherId = Guid.NewGuid();
        CallerGates(sutProvider, userId, gatedCipherId);
        HasNoActiveLeases(sutProvider, userId);

        // A half-applied bulk delete would leave the caller unable to tell what happened.
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.EnsureCanMutateManyAsync(
            userId, [new Cipher { Id = plainCipherId }, new Cipher { Id = gatedCipherId }]));
    }

    [Fact]
    public async Task EnsureCanMutateManyAsync_GatedWithActiveLease_Authorizes()
    {
        var (sutProvider, userId, gatedCipherId) = Setup();
        CallerGates(sutProvider, userId, gatedCipherId);
        HasActiveLeasesFor(sutProvider, userId, gatedCipherId);

        var access = await sutProvider.Sut.EnsureCanMutateManyAsync(
            userId, [new Cipher { Id = gatedCipherId }]);

        // Unlike the bulk read, which withholds a gated cipher whatever the lease state.
        Assert.True(access.Authorizes(gatedCipherId));
    }

    [Fact]
    public async Task EnsureCanMutateManyAsync_GovernedOnlyByDisabledRule_NotGated()
    {
        var (sutProvider, userId, cipherId) = Setup();
        var disabledRuleCollectionId = Guid.NewGuid();
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(userId)
            .Returns([DisabledRuleCollection(disabledRuleCollectionId)]);
        sutProvider.GetDependency<ICollectionCipherRepository>().GetManyByUserIdAsync(userId)
            .Returns([new CollectionCipher { CipherId = cipherId, CollectionId = disabledRuleCollectionId }]);

        var access = await sutProvider.Sut.EnsureCanMutateManyAsync(userId, [new Cipher { Id = cipherId }]);

        Assert.True(access.Authorizes(cipherId));
    }

    [Fact]
    public async Task EnsureCanMutateManyAsync_QueryCountIsIndependentOfBatchSize()
    {
        var (sutProvider, userId, firstCipherId) = Setup();
        var secondCipherId = Guid.NewGuid();
        var plainCipherId = Guid.NewGuid();
        CallerGates(sutProvider, userId, firstCipherId, secondCipherId);
        HasActiveLeasesFor(sutProvider, userId, firstCipherId, secondCipherId);

        // MoveManyAsync forwards request ids straight through, so duplicates reach the gate.
        await sutProvider.Sut.EnsureCanMutateManyAsync(userId,
        [
            new Cipher { Id = firstCipherId }, new Cipher { Id = secondCipherId },
            new Cipher { Id = plainCipherId }, new Cipher { Id = firstCipherId }
        ]);

        await sutProvider.GetDependency<ICollectionRepository>().Received(1).GetManyByUserIdAsync(userId);
        await sutProvider.GetDependency<ICollectionCipherRepository>().Received(1).GetManyByUserIdAsync(userId);
        await sutProvider.GetDependency<IAccessLeaseRepository>().Received(1)
            .GetManyActiveByRequesterIdAsync(userId, _now);
        await sutProvider.GetDependency<IGoverningRuleResolver>()
            .DidNotReceiveWithAnyArgs().ResolveAsync(default, default, default!);
    }

    [Fact]
    public void UnrestrictedForWholeVaultExport_AuthorizesAnyCipher()
    {
        var (sutProvider, _, _) = Setup();

        var access = sutProvider.Sut.UnrestrictedForWholeVaultExport();

        Assert.True(access.Authorizes(Guid.NewGuid()));
    }

    [Fact]
    public async Task AuthorizeAdminReadAsync_FlagOff_AuthorizesWithoutQuerying()
    {
        var (sutProvider, userId, cipherId) = Setup(enabled: false);

        var access = await sutProvider.Sut.AuthorizeAdminReadAsync(userId, Guid.NewGuid(), new Cipher { Id = cipherId, OrganizationId = _organizationId });

        Assert.NotNull(access);
        Assert.True(access.Authorizes(cipherId));
        await sutProvider.GetDependency<IGatingCollectionResolver>().DidNotReceiveWithAnyArgs()
            .GetGatingCollectionIdsAsync(default);
    }

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

        var access = await sutProvider.Sut.AuthorizeAdminReadAsync(userId, organizationId, new Cipher { Id = cipherId, OrganizationId = _organizationId });

        Assert.Null(access);
    }

    [Fact]
    public async Task AuthorizeAdminReadAsync_GatedWithActiveLease_Authorizes()
    {
        var (sutProvider, userId, cipherId) = Setup();
        // The admin read resolves licensing against the route organization.
        var organizationId = _organizationId;
        var collectionId = Guid.NewGuid();
        OrganizationLeasingCollection(sutProvider, organizationId, collectionId);
        CipherIsInCollections(sutProvider, cipherId, collectionId);
        HasActiveLease(sutProvider, userId, cipherId);

        var access = await sutProvider.Sut.AuthorizeAdminReadAsync(userId, organizationId, new Cipher { Id = cipherId, OrganizationId = _organizationId });

        Assert.NotNull(access);
        Assert.True(access.Authorizes(cipherId));
    }

    [Fact]
    public async Task AuthorizeReadAsync_GatedWithActiveLease_Unlicensed_Blocks()
    {
        var (sutProvider, userId, cipherId) = Setup();
        Gated(sutProvider, userId, cipherId);
        HasActiveLease(sutProvider, userId, cipherId);
        Unlicensed(sutProvider);

        // A lease minted before the license was withdrawn is not an exemption.
        Assert.Null(await sutProvider.Sut.AuthorizeReadAsync(userId, new Cipher { Id = cipherId, OrganizationId = _organizationId }));
    }

    [Fact]
    public async Task AuthorizeReadAsync_GatedWithActiveLease_Unlicensed_DoesNotReadTheLease()
    {
        var (sutProvider, userId, cipherId) = Setup();
        Gated(sutProvider, userId, cipherId);
        Unlicensed(sutProvider);

        await sutProvider.Sut.AuthorizeReadAsync(userId, new Cipher { Id = cipherId, OrganizationId = _organizationId });

        // The entitlement is settled from claims, so an unlicensed caller skips the lease query.
        await sutProvider.GetDependency<IAccessLeaseRepository>().DidNotReceiveWithAnyArgs()
            .GetActiveByRequesterIdCipherIdAsync(default, default, default);
    }

    [Fact]
    public async Task AuthorizeReadAsync_NotGated_Unlicensed_Authorizes()
    {
        var (sutProvider, userId, cipherId) = Setup();
        NotGated(sutProvider, userId, cipherId);
        Unlicensed(sutProvider);

        // Licensing narrows what a lease can release; it does not gate a cipher no rule governs.
        var access = await sutProvider.Sut.AuthorizeReadAsync(userId, new Cipher { Id = cipherId, OrganizationId = _organizationId });

        Assert.NotNull(access);
        Assert.True(access.Authorizes(cipherId));
    }

    [Fact]
    public async Task EnsureCanMutateAsync_GatedWithActiveLease_Unlicensed_Throws()
    {
        var (sutProvider, userId, cipherId) = Setup();
        Gated(sutProvider, userId, cipherId);
        HasActiveLease(sutProvider, userId, cipherId);
        Unlicensed(sutProvider);

        // A lease that no longer releases the credential does not authorize writing it either.
        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.EnsureCanMutateAsync(userId, new Cipher { Id = cipherId, OrganizationId = _organizationId }));
    }

    [Fact]
    public async Task EnsureCanMutateManyAsync_GatedWithActiveLease_Unlicensed_Throws()
    {
        var (sutProvider, userId, cipherId) = Setup();
        CallerGates(sutProvider, userId, cipherId);
        HasActiveLeasesFor(sutProvider, userId, cipherId);
        Unlicensed(sutProvider);

        // Licensing is read off the lease, so a cipher passed without its organization is still covered.
        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.EnsureCanMutateManyAsync(userId, [new Cipher { Id = cipherId }]));
    }

    [Fact]
    public async Task AuthorizeAdminReadAsync_GatedWithActiveLease_Unlicensed_Blocks()
    {
        var (sutProvider, userId, cipherId) = Setup();
        var collectionId = Guid.NewGuid();
        OrganizationLeasingCollection(sutProvider, _organizationId, collectionId);
        CipherIsInCollections(sutProvider, cipherId, collectionId);
        HasActiveLease(sutProvider, userId, cipherId);
        Unlicensed(sutProvider);

        Assert.Null(await sutProvider.Sut.AuthorizeAdminReadAsync(
            userId, _organizationId, new Cipher { Id = cipherId, OrganizationId = _organizationId }));
    }

    [Fact]
    public async Task AuthorizeAdminReadAsync_DisabledRule_Authorizes()
    {
        var (sutProvider, userId, cipherId) = Setup();
        var organizationId = Guid.NewGuid();
        var collectionId = Guid.NewGuid();
        OrganizationLeasingCollection(sutProvider, organizationId, collectionId, ruleEnabled: false);
        CipherIsInCollections(sutProvider, cipherId, collectionId);

        var access = await sutProvider.Sut.AuthorizeAdminReadAsync(userId, organizationId, new Cipher { Id = cipherId, OrganizationId = _organizationId });

        Assert.NotNull(access);
        Assert.True(access.Authorizes(cipherId));
    }

    /// <remarks>
    /// An unassigned cipher is in no collection, so nothing gates it; admin endpoints reach it through
    /// <c>CanAccessUnassignedCiphersAsync</c>.
    /// </remarks>
    [Fact]
    public async Task AuthorizeAdminReadAsync_UnassignedCipher_Authorizes()
    {
        var (sutProvider, userId, cipherId) = Setup();
        var organizationId = Guid.NewGuid();
        OrganizationLeasingCollection(sutProvider, organizationId, Guid.NewGuid());
        CipherIsInCollections(sutProvider, cipherId);

        var access = await sutProvider.Sut.AuthorizeAdminReadAsync(userId, organizationId, new Cipher { Id = cipherId, OrganizationId = _organizationId });

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

        var access = await sutProvider.Sut.AuthorizeAdminReadAsync(userId, organizationId, new Cipher { Id = cipherId, OrganizationId = _organizationId });

        Assert.NotNull(access);
        Assert.True(access.Authorizes(cipherId));
    }

    /// <remarks>Matches the member bulk read, since secrets are released one cipher at a time.</remarks>
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
        sutProvider.GetDependency<IGatingCollectionResolver>()
            .GetGatingCollectionIdsAsync(organizationId)
            .Returns(new HashSet<Guid>());

        var access = await sutProvider.Sut.AuthorizeAdminReadManyAsync(userId, organizationId,
            [new Cipher { Id = cipherId, OrganizationId = _organizationId }]);

        Assert.True(access.Authorizes(cipherId));
        await sutProvider.GetDependency<ICollectionCipherRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdAsync(default);
    }

    private static void OrganizationLeasingCollection(SutProvider<CipherLeaseGate> sutProvider,
        Guid organizationId, Guid collectionId, bool ruleEnabled = true) =>
        sutProvider.GetDependency<IGatingCollectionResolver>()
            .GetGatingCollectionIdsAsync(organizationId)
            .Returns(ruleEnabled ? new HashSet<Guid> { collectionId } : new HashSet<Guid>());

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
        // Licensed by default; most cases are about gating and leases, not entitlement.
        sutProvider.GetDependency<ICurrentContext>().AccessPam(_organizationId).Returns(true);
        return (sutProvider, Guid.NewGuid(), Guid.NewGuid());
    }

    private static void Unlicensed(SutProvider<CipherLeaseGate> sutProvider) =>
        sutProvider.GetDependency<ICurrentContext>().AccessPam(_organizationId).Returns(false);

    private static void Gated(SutProvider<CipherLeaseGate> sutProvider, Guid userId, Guid cipherId) =>
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>())
            .Returns(new GoverningRule(Guid.NewGuid(), Guid.NewGuid(), RequiresHumanApproval: false,
                Array.Empty<AccessCondition>()));

    private static void NotGated(SutProvider<CipherLeaseGate> sutProvider, Guid userId, Guid cipherId) =>
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>())
            .Returns((GoverningRule?)null);

    private static void CallerGates(SutProvider<CipherLeaseGate> sutProvider, Guid userId, params Guid[] gatedCipherIds)
    {
        var leasingCollectionId = Guid.NewGuid();
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(userId)
            .Returns([LeasingCollection(leasingCollectionId)]);
        sutProvider.GetDependency<ICollectionCipherRepository>().GetManyByUserIdAsync(userId)
            .Returns(gatedCipherIds.Select(id => new CollectionCipher { CipherId = id, CollectionId = leasingCollectionId }).ToList());
    }

    private static void HasActiveLease(SutProvider<CipherLeaseGate> sutProvider, Guid userId, Guid cipherId) =>
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, Arg.Any<DateTime>())
            .Returns(new AccessLease { CipherId = cipherId });

    private static void HasActiveLeasesFor(SutProvider<CipherLeaseGate> sutProvider, Guid userId,
        params Guid[] cipherIds) =>
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetManyActiveByRequesterIdAsync(userId, Arg.Any<DateTime>())
            .Returns(cipherIds.Select(id => new AccessLease { CipherId = id, OrganizationId = _organizationId }).ToList());

    private static void HasNoActiveLeases(SutProvider<CipherLeaseGate> sutProvider, Guid userId) =>
        HasActiveLeasesFor(sutProvider, userId);

    private static CollectionDetails LeasingCollection(Guid id) =>
        new() { Id = id, AccessRuleId = Guid.NewGuid(), HasEnabledAccessRule = true };

    private static CollectionDetails DisabledRuleCollection(Guid id) =>
        new() { Id = id, AccessRuleId = Guid.NewGuid(), HasEnabledAccessRule = false };

    private static CollectionDetails PlainCollection(Guid id) => new() { Id = id, AccessRuleId = null };

    private static IDictionary<Guid, IGrouping<Guid, CollectionCipher>> Group(
        params CollectionCipher[] collectionCiphers) =>
        collectionCiphers.GroupBy(cc => cc.CipherId).ToDictionary(g => g.Key);
}
