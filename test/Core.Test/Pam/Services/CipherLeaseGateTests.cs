using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Pam.Engine;
using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Models;
using Bit.Core.Pam.Models.Conditions;
using Bit.Core.Pam.Repositories;
using Bit.Core.Pam.Services;
using Bit.Core.Services;
using Bit.Core.Vault.Entities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Pam.Services;

[SutProviderCustomize]
public class CipherLeaseGateTests
{
    private static void EnableFlag(SutProvider<CipherLeaseGate> sutProvider) =>
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.Pam).Returns(true);

    private static void Gated(SutProvider<CipherLeaseGate> sutProvider, Guid userId, Guid cipherId) =>
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>())
            .Returns(new GoverningRule(Guid.NewGuid(), Guid.NewGuid(), RequiresHumanApproval: false,
                Array.Empty<AccessCondition>()));

    private static void HasActiveLease(SutProvider<CipherLeaseGate> sutProvider, Guid userId, Guid cipherId) =>
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, Arg.Any<DateTime>())
            .Returns(new AccessLease { CipherId = cipherId });

    // --- AuthorizeReadAsync ------------------------------------------------------------------------

    [Theory, BitAutoData]
    public async Task AuthorizeReadAsync_FlagOff_AuthorizesWithoutQuerying(
        SutProvider<CipherLeaseGate> sutProvider, Guid userId, Guid cipherId)
    {
        var access = await sutProvider.Sut.AuthorizeReadAsync(userId, new Cipher { Id = cipherId });

        Assert.NotNull(access);
        Assert.True(access!.Authorizes(cipherId));
        await sutProvider.GetDependency<IGoverningRuleResolver>()
            .DidNotReceiveWithAnyArgs().ResolveAsync(default, default, default!);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeReadAsync_NotGated_Authorizes(
        SutProvider<CipherLeaseGate> sutProvider, Guid userId, Guid cipherId)
    {
        EnableFlag(sutProvider);
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>())
            .Returns((GoverningRule?)null);

        var access = await sutProvider.Sut.AuthorizeReadAsync(userId, new Cipher { Id = cipherId });

        Assert.NotNull(access);
        Assert.True(access!.Authorizes(cipherId));
    }

    [Theory, BitAutoData]
    public async Task AuthorizeReadAsync_GatedNoLease_ReturnsNull(
        SutProvider<CipherLeaseGate> sutProvider, Guid userId, Guid cipherId)
    {
        EnableFlag(sutProvider);
        Gated(sutProvider, userId, cipherId);

        var access = await sutProvider.Sut.AuthorizeReadAsync(userId, new Cipher { Id = cipherId });

        Assert.Null(access);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeReadAsync_GatedWithLease_Authorizes(
        SutProvider<CipherLeaseGate> sutProvider, Guid userId, Guid cipherId)
    {
        EnableFlag(sutProvider);
        Gated(sutProvider, userId, cipherId);
        HasActiveLease(sutProvider, userId, cipherId);

        var access = await sutProvider.Sut.AuthorizeReadAsync(userId, new Cipher { Id = cipherId });

        Assert.NotNull(access);
        Assert.True(access!.Authorizes(cipherId));
    }

    // --- GetGatedCipherIds -------------------------------------------------------------------------

    [Theory, BitAutoData]
    public void GetGatedCipherIds_FlagOff_Empty(
        SutProvider<CipherLeaseGate> sutProvider, Guid leasingCollectionId, Guid cipherId)
    {
        var collections = new[] { new CollectionDetails { Id = leasingCollectionId, AccessRuleId = Guid.NewGuid() } };
        var mappings = Group(new CollectionCipher { CipherId = cipherId, CollectionId = leasingCollectionId });

        var gated = sutProvider.Sut.GetGatedCipherIds(collections, mappings);

        Assert.Empty(gated);
    }

    [Theory, BitAutoData]
    public void GetGatedCipherIds_ReachableOnlyThroughLeasingCollection_IsGated(
        SutProvider<CipherLeaseGate> sutProvider, Guid leasingCollectionId, Guid cipherId)
    {
        EnableFlag(sutProvider);
        var collections = new[] { new CollectionDetails { Id = leasingCollectionId, AccessRuleId = Guid.NewGuid() } };
        var mappings = Group(new CollectionCipher { CipherId = cipherId, CollectionId = leasingCollectionId });

        var gated = sutProvider.Sut.GetGatedCipherIds(collections, mappings);

        Assert.Contains(cipherId, gated);
    }

    [Theory, BitAutoData]
    public void GetGatedCipherIds_AlsoReachableThroughNonLeasingCollection_NotGated(
        SutProvider<CipherLeaseGate> sutProvider, Guid leasingCollectionId, Guid plainCollectionId, Guid cipherId)
    {
        EnableFlag(sutProvider);
        var collections = new[]
        {
            new CollectionDetails { Id = leasingCollectionId, AccessRuleId = Guid.NewGuid() },
            new CollectionDetails { Id = plainCollectionId, AccessRuleId = null },
        };
        var mappings = Group(
            new CollectionCipher { CipherId = cipherId, CollectionId = leasingCollectionId },
            new CollectionCipher { CipherId = cipherId, CollectionId = plainCollectionId });

        var gated = sutProvider.Sut.GetGatedCipherIds(collections, mappings);

        Assert.DoesNotContain(cipherId, gated);
    }

    // --- AuthorizeReadManyAsync --------------------------------------------------------------------

    [Theory, BitAutoData]
    public async Task AuthorizeReadManyAsync_AuthorizesNonGatedOnly(
        SutProvider<CipherLeaseGate> sutProvider, Guid userId, Guid leasingCollectionId, Guid gatedCipherId, Guid plainCipherId)
    {
        EnableFlag(sutProvider);
        var collections = new[] { new CollectionDetails { Id = leasingCollectionId, AccessRuleId = Guid.NewGuid() } };
        var mappings = Group(new CollectionCipher { CipherId = gatedCipherId, CollectionId = leasingCollectionId });
        var ciphers = new[] { new Cipher { Id = gatedCipherId }, new Cipher { Id = plainCipherId } };

        var access = await sutProvider.Sut.AuthorizeReadManyAsync(userId, ciphers, collections, mappings);

        Assert.False(access.Authorizes(gatedCipherId));
        Assert.True(access.Authorizes(plainCipherId));
    }

    // --- EnsureCanMutateAsync ----------------------------------------------------------------------

    [Theory, BitAutoData]
    public async Task EnsureCanMutateAsync_FlagOff_DoesNotThrow(
        SutProvider<CipherLeaseGate> sutProvider, Guid userId, Guid cipherId)
    {
        var access = await sutProvider.Sut.EnsureCanMutateAsync(userId, new Cipher { Id = cipherId });
        Assert.True(access.Authorizes(cipherId));
    }

    [Theory, BitAutoData]
    public async Task EnsureCanMutateAsync_GatedNoLease_ThrowsNotFound(
        SutProvider<CipherLeaseGate> sutProvider, Guid userId, Guid cipherId)
    {
        EnableFlag(sutProvider);
        Gated(sutProvider, userId, cipherId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.EnsureCanMutateAsync(userId, new Cipher { Id = cipherId }));
    }

    [Theory, BitAutoData]
    public async Task EnsureCanMutateAsync_GatedWithLease_DoesNotThrow(
        SutProvider<CipherLeaseGate> sutProvider, Guid userId, Guid cipherId)
    {
        EnableFlag(sutProvider);
        Gated(sutProvider, userId, cipherId);
        HasActiveLease(sutProvider, userId, cipherId);

        var access = await sutProvider.Sut.EnsureCanMutateAsync(userId, new Cipher { Id = cipherId });

        Assert.True(access.Authorizes(cipherId));
    }

    // --- EnsureCanMutateManyAsync ------------------------------------------------------------------

    [Theory, BitAutoData]
    public async Task EnsureCanMutateManyAsync_LeasedCipher_SkipsResolve(
        SutProvider<CipherLeaseGate> sutProvider, Guid userId, Guid cipherId)
    {
        EnableFlag(sutProvider);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetManyActiveByRequesterIdAsync(userId, Arg.Any<DateTime>())
            .Returns(new List<AccessLease> { new() { CipherId = cipherId } });

        await sutProvider.Sut.EnsureCanMutateManyAsync(userId, new[] { new Cipher { Id = cipherId } });

        await sutProvider.GetDependency<IGoverningRuleResolver>()
            .DidNotReceiveWithAnyArgs().ResolveAsync(default, default, default!);
    }

    [Theory, BitAutoData]
    public async Task EnsureCanMutateManyAsync_OneGatedNoLease_ThrowsNotFound(
        SutProvider<CipherLeaseGate> sutProvider, Guid userId, Guid gatedCipherId, Guid plainCipherId)
    {
        EnableFlag(sutProvider);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetManyActiveByRequesterIdAsync(userId, Arg.Any<DateTime>())
            .Returns(new List<AccessLease>());
        Gated(sutProvider, userId, gatedCipherId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.EnsureCanMutateManyAsync(userId,
                new[] { new Cipher { Id = plainCipherId }, new Cipher { Id = gatedCipherId } }));
    }

    private static IDictionary<Guid, IGrouping<Guid, CollectionCipher>> Group(params CollectionCipher[] collectionCiphers) =>
        collectionCiphers.GroupBy(cc => cc.CipherId).ToDictionary(g => g.Key);
}
