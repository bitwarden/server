using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Bit.Seeder;
using Bit.Seeder.Scenes;
using Bit.Seeder.Services;
using NSubstitute;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

public class OrganizationAccessRuleSceneGuardWiringTests
{
    private readonly Organization _organization = new() { Id = Guid.NewGuid() };
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly ICollectionRepository _collectionRepository = Substitute.For<ICollectionRepository>();
    private readonly IAccessRuleRepository _accessRuleRepository = Substitute.For<IAccessRuleRepository>();
    private readonly IManglerService _manglerService = Substitute.For<IManglerService>();

    public OrganizationAccessRuleSceneGuardWiringTests()
    {
        _organizationRepository.GetByIdAsync(_organization.Id).Returns(_organization);
        _manglerService.GetMangleMap().Returns([]);
    }

    [Fact]
    public async Task SeedAsync_ForeignCollection_ThrowsWithoutCreatingRule()
    {
        var foreignCollection = new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), Name = "c" };
        _collectionRepository.GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([foreignCollection]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => SeedAsync(foreignCollection.Id));

        Assert.Contains("not in organization", exception.Message);
        await _accessRuleRepository.DidNotReceive().CreateAsync(Arg.Any<AccessRule>());
    }

    [Fact]
    public async Task SeedAsync_GovernedCollection_ThrowsWithoutCreatingRule()
    {
        var governedCollection = new Collection
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organization.Id,
            Name = "c",
            AccessRuleId = Guid.NewGuid()
        };
        _collectionRepository.GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([governedCollection]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => SeedAsync(governedCollection.Id));

        Assert.Contains("already governed by an access rule", exception.Message);
        await _collectionRepository.Received(1).GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>());
        await _accessRuleRepository.DidNotReceive().CreateAsync(Arg.Any<AccessRule>());
    }

    [Fact]
    public async Task SeedAsync_DuplicateCollectionIds_AssociatesDistinctIds()
    {
        var collection = new Collection { Id = Guid.NewGuid(), OrganizationId = _organization.Id, Name = "c" };
        _collectionRepository.GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([collection]);

        var result = await SeedAsync(collection.Id, collection.Id);

        await _collectionRepository.Received(1).GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>());
        await _collectionRepository.Received(1).SetAccessRuleAssociationsAsync(
            _organization.Id,
            result.Result.AccessRuleId,
            Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { collection.Id })),
            Arg.Any<IEnumerable<Guid>>());
    }

    private Task<SceneResult<OrganizationAccessRuleScene.Result>> SeedAsync(params Guid[] collectionIds) =>
        new OrganizationAccessRuleScene(_organizationRepository, _collectionRepository, _accessRuleRepository, _manglerService)
            .SeedAsync(new OrganizationAccessRuleScene.Request
            {
                OrganizationId = _organization.Id,
                Name = "Rule",
                CollectionIds = collectionIds
            });
}
