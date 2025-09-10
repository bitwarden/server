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

        var ciphers = new List<CipherOrganizationDetails>
        {
            otherCipher,    // not in the target collection
            targetCipher,   // in the target collection
            bothCipher,     // in both collections
            noCipher        // not in any collection
        };
        ciphers.ForEach(c =>
        {
            c.OrganizationId = organizationId;
            c.UserId = null;
        });

        var otherCollectionCipher = new CollectionCipher
        {
            CollectionId = otherCollectionId,
            CipherId = otherCipher.Id
        };
        var targetCollectionCipher = new CollectionCipher
        {
            CollectionId = targetCollectionId,
            CipherId = targetCipher.Id
        };
        var bothCollectionCipher1 = new CollectionCipher
        {
            CollectionId = targetCollectionId,
            CipherId = bothCipher.Id
        };
        var bothCollectionCipher2 = new CollectionCipher
        {
            CollectionId = otherCollectionId,
            CipherId = bothCipher.Id
        };

        sutProvider.GetDependency<ICipherRepository>().GetManyOrganizationDetailsByOrganizationIdAsync(organizationId)
            .Returns(ciphers);

        sutProvider.GetDependency<ICollectionCipherRepository>().GetManyByOrganizationIdAsync(organizationId).Returns(
        [
            targetCollectionCipher,
            otherCollectionCipher,
            bothCollectionCipher1,
            bothCollectionCipher2
        ]);

        var result = await sutProvider
            .Sut
            .GetOrganizationCiphersByCollectionIds(organizationId, [targetCollectionId]);
        result = result.ToList();

        Assert.Equal(2, result.Count());
        Assert.Contains(result, c =>
            c.Id == targetCipher.Id &&
            c.CollectionIds.Count() == 1 &&
            c.CollectionIds.Any(cId => cId == targetCollectionId));
        Assert.Contains(result, c =>
            c.Id == bothCipher.Id &&
            c.CollectionIds.Count() == 2 &&
            c.CollectionIds.Any(cId => cId == targetCollectionId) &&
            c.CollectionIds.Any(cId => cId == otherCollectionId));
    }


    [Theory, BitAutoData]
    public async Task GetAllOrganizationCiphersExcludingDefaultUserCollections_DelegatesToRepository(
         Guid organizationId,
         SutProvider<OrganizationCiphersQuery> sutProvider)
    {
        var item1 = new CipherOrganizationDetailsWithCollections(
            new CipherOrganizationDetails { Id = Guid.NewGuid(), OrganizationId = organizationId },
            new Dictionary<Guid, IGrouping<Guid, CollectionCipher>>());
        var item2 = new CipherOrganizationDetailsWithCollections(
            new CipherOrganizationDetails { Id = Guid.NewGuid(), OrganizationId = organizationId },
            new Dictionary<Guid, IGrouping<Guid, CollectionCipher>>());

        var repo = sutProvider.GetDependency<ICipherRepository>();
        repo.GetManyCipherOrganizationDetailsExcludingDefaultCollectionsAsync(organizationId)
            .Returns(Task.FromResult<IEnumerable<CipherOrganizationDetailsWithCollections>>(
                new[] { item1, item2 }));

        var actual = (await sutProvider.Sut
            .GetAllOrganizationCiphersExcludingDefaultUserCollections(organizationId))
            .ToList();

        Assert.Equal(2, actual.Count);
        Assert.Same(item1, actual[0]);
        Assert.Same(item2, actual[1]);

        // and we indeed called the repo once
        await repo.Received(1)
            .GetManyCipherOrganizationDetailsExcludingDefaultCollectionsAsync(organizationId);
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
