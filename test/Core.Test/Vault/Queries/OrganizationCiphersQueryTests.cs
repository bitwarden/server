using AutoFixture;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Queries;
using Bit.Core.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Vault.Queries;

[SutProviderCustomize]
public class OrganizationCiphersQueryTests
{
    [Theory, BitAutoData]
    public async Task GetOrganizationCiphersInCollections_ReturnsFilteredCiphers(
        Guid organizationId, SutProvider<OrganizationCiphersQuery> sutProvider)
    {
        var fixture = new Fixture();
        var otherCollectionId = Guid.NewGuid();
        var targetCollectionId = Guid.NewGuid();

        var otherCipher = fixture.Create<CipherOrganizationDetails>();
        var targetCipher = fixture.Create<CipherOrganizationDetails>();
        var bothCipher = fixture.Create<CipherOrganizationDetails>();
        var noCipher = fixture.Create<CipherOrganizationDetails>();

        foreach (var c in new[] { otherCipher, targetCipher, bothCipher, noCipher })
        {
            c.OrganizationId = organizationId;
            c.UserId = null;
        }

        var otherItem = MakeWith(otherCipher);
        var targetItem = MakeWith(targetCipher, targetCollectionId);
        var bothItem = MakeWith(bothCipher, targetCollectionId, otherCollectionId);
        var noItem = MakeWith(noCipher);

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyOrganizationDetailsWithCollectionsByOrganizationIdAsync(organizationId)
            .Returns(new[] { otherItem, targetItem, bothItem, noItem });

        var result = (await sutProvider.Sut
            .GetOrganizationCiphersByCollectionIds(organizationId, new[] { targetCollectionId }))
            .ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, c =>
            c.Id == targetCipher.Id &&
            c.CollectionIds.SequenceEqual(new[] { targetCollectionId }));
        Assert.Contains(result, c =>
            c.Id == bothCipher.Id &&
            c.CollectionIds.OrderBy(x => x).SequenceEqual(
                new[] { otherCollectionId, targetCollectionId }.OrderBy(x => x)));
    }

    [Theory, BitAutoData]
    public async Task GetAllOrganizationCiphersExcludingDefaultUserCollections_ExcludesCiphersInDefaultCollections(
        Guid organizationId, SutProvider<OrganizationCiphersQuery> sutProvider)
    {
        var defaultColId = Guid.NewGuid();
        var otherColId = Guid.NewGuid();

        var cipherDefault = new CipherOrganizationDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            OrganizationUseTotp = false
        };
        var cipherOther = new CipherOrganizationDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            OrganizationUseTotp = false
        };

        var defaultItem = MakeWith(cipherDefault, defaultColId);
        var otherItem = MakeWith(cipherOther, otherColId);

        // stub just the default‐ID lookup
        sutProvider.GetDependency<ICollectionRepository>()
            .GetDefaultCollectionIdsByOrganizationIdAsync(organizationId)
            .Returns(new[] { defaultColId });

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyOrganizationDetailsWithCollectionsByOrganizationIdAsync(organizationId)
            .Returns(new[] { defaultItem, otherItem });

        var result = (await sutProvider.Sut
            .GetAllOrganizationCiphersExcludingDefaultUserCollections(organizationId))
            .ToList();

        Assert.Single(result);
        Assert.Equal(cipherOther.Id, result.Single().Id);
    }

    private CipherOrganizationDetailsWithCollections MakeWith(
        CipherOrganizationDetails baseCipher,
        params Guid[] cols)
    {
        var dict = cols
          .Select(cid => new CollectionCipher { CipherId = baseCipher.Id, CollectionId = cid })
          .GroupBy(cc => cc.CipherId)
          .ToDictionary(g => g.Key, g => g);

        return new CipherOrganizationDetailsWithCollections(baseCipher, dict);
    }
}
