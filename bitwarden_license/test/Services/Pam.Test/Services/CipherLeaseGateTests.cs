using Bit.Core;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Services;
using Bit.Core.Vault.Entities;
using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Models;
using Bit.Services.Pam.Models.Conditions;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Services;

// Covers the read-path gate only. Write-path gating (EnsureCanMutate*) is deferred with the rest of the
// mutation-refusal work, and GetGatedCipherIds is now a private detail exercised through AuthorizeReadManyAsync.
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

    private static IDictionary<Guid, IGrouping<Guid, CollectionCipher>> Group(params CollectionCipher[] ccs) =>
        ccs.GroupBy(cc => cc.CipherId).ToDictionary(g => g.Key);

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

    // --- AuthorizeReadManyAsync (also covers the private gated-id computation) ----------------------

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

    [Theory, BitAutoData]
    public async Task AuthorizeReadManyAsync_ReachableOnlyThroughLeasingCollection_IsGated(
        SutProvider<CipherLeaseGate> sutProvider, Guid userId, Guid leasingCollectionId, Guid cipherId)
    {
        EnableFlag(sutProvider);
        var collections = new[] { new CollectionDetails { Id = leasingCollectionId, AccessRuleId = Guid.NewGuid() } };
        var mappings = Group(new CollectionCipher { CipherId = cipherId, CollectionId = leasingCollectionId });

        var access = await sutProvider.Sut.AuthorizeReadManyAsync(
            userId, new[] { new Cipher { Id = cipherId } }, collections, mappings);

        Assert.False(access.Authorizes(cipherId));
    }

    [Theory, BitAutoData]
    public async Task AuthorizeReadManyAsync_AlsoReachableThroughNonLeasingCollection_NotGated(
        SutProvider<CipherLeaseGate> sutProvider, Guid userId, Guid leasingCollectionId, Guid plainCollectionId, Guid cipherId)
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

        var access = await sutProvider.Sut.AuthorizeReadManyAsync(
            userId, new[] { new Cipher { Id = cipherId } }, collections, mappings);

        Assert.True(access.Authorizes(cipherId));
    }
}
