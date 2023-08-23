using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationCollections;
using Bit.Core.Repositories;
using Bit.Core.Test.Vault.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationCollections;

[SutProviderCustomize]
public class BulkAddCollectionAccessCommandTests
{
    [Theory, BitAutoData, CollectionCustomization]
    public async Task ValidateRequestAsync_Success(SutProvider<BulkAddCollectionAccessCommand> sutProvider,
        Organization org,
        ICollection<Collection> collections,
        ICollection<OrganizationUser> users,
        ICollection<Group> groups,
        IEnumerable<CollectionUser> collectionUsers,
        IEnumerable<CollectionGroup> collectionGroups)
    {
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(collections);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(users);

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByManyIds(Arg.Any<IEnumerable<Guid>>())
            .Returns(groups);

        await sutProvider.Sut.AddAccessAsync(org.Id, collections.Select(c => c.Id).ToList(),
            ToAccessSelection(collectionUsers),
            ToAccessSelection(collectionGroups)
        );

        await sutProvider.GetDependency<ICollectionRepository>().ReceivedWithAnyArgs().GetManyByManyIdsAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>().ReceivedWithAnyArgs().GetManyAsync(default);
        await sutProvider.GetDependency<IGroupRepository>().ReceivedWithAnyArgs().GetManyByManyIds(default);
    }


    [Theory, BitAutoData, CollectionCustomization]
    public async Task ValidateRequestAsync_MissingCollection_Failure(SutProvider<BulkAddCollectionAccessCommand> sutProvider,
        Organization org,
        IList<Collection> collections,
        IEnumerable<CollectionUser> collectionUsers,
        IEnumerable<CollectionGroup> collectionGroups)
    {
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(collections.Skip(1).ToList());

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.AddAccessAsync(org.Id, collections.Select(c => c.Id).ToList(),
            ToAccessSelection(collectionUsers),
            ToAccessSelection(collectionGroups)
        ));

        Assert.Contains("One or more collections do not exist.", exception.Message);

        await sutProvider.GetDependency<ICollectionRepository>().ReceivedWithAnyArgs().GetManyByManyIdsAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs().GetManyAsync(default);
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().GetManyByManyIds(default);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task ValidateRequestAsync_MissingUser_Failure(SutProvider<BulkAddCollectionAccessCommand> sutProvider,
        Organization org,
        IList<Collection> collections,
        IList<OrganizationUser> users,
        IEnumerable<CollectionUser> collectionUsers,
        IEnumerable<CollectionGroup> collectionGroups)
    {
        users.RemoveAt(0);

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(collections);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(users);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.AddAccessAsync(org.Id, collections.Select(c => c.Id).ToList(),
            ToAccessSelection(collectionUsers),
            ToAccessSelection(collectionGroups)
        ));

        Assert.Contains("One or more users do not exist.", exception.Message);

        await sutProvider.GetDependency<ICollectionRepository>().ReceivedWithAnyArgs().GetManyByManyIdsAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>().ReceivedWithAnyArgs().GetManyAsync(default);
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().GetManyByManyIds(default);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task ValidateRequestAsync_UserWrongOrg_Failure(SutProvider<BulkAddCollectionAccessCommand> sutProvider,
        Organization org,
        IList<Collection> collections,
        IList<OrganizationUser> users,
        IEnumerable<CollectionUser> collectionUsers,
        IEnumerable<CollectionGroup> collectionGroups)
    {
        users.First().OrganizationId = Guid.NewGuid();

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(collections);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(users);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.AddAccessAsync(org.Id, collections.Select(c => c.Id).ToList(),
            ToAccessSelection(collectionUsers),
            ToAccessSelection(collectionGroups)
        ));

        Assert.Contains("One or more users do not belong to the same organization as the collection being assigned.", exception.Message);

        await sutProvider.GetDependency<ICollectionRepository>().ReceivedWithAnyArgs().GetManyByManyIdsAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>().ReceivedWithAnyArgs().GetManyAsync(default);
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().GetManyByManyIds(default);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task ValidateRequestAsync_MissingGroup_Failure(SutProvider<BulkAddCollectionAccessCommand> sutProvider,
        Organization org,
        IList<Collection> collections,
        IList<OrganizationUser> users,
        IList<Group> groups,
        IEnumerable<CollectionUser> collectionUsers,
        IEnumerable<CollectionGroup> collectionGroups)
    {
        groups.RemoveAt(0);

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(collections);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(users);

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByManyIds(Arg.Any<IEnumerable<Guid>>())
            .Returns(groups);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.AddAccessAsync(org.Id, collections.Select(c => c.Id).ToList(),
            ToAccessSelection(collectionUsers),
            ToAccessSelection(collectionGroups)
        ));

        Assert.Contains("One or more groups do not exist.", exception.Message);

        await sutProvider.GetDependency<ICollectionRepository>().ReceivedWithAnyArgs().GetManyByManyIdsAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>().ReceivedWithAnyArgs().GetManyAsync(default);
        await sutProvider.GetDependency<IGroupRepository>().ReceivedWithAnyArgs().GetManyByManyIds(default);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task ValidateRequestAsync_GroupWrongOrg_Failure(SutProvider<BulkAddCollectionAccessCommand> sutProvider,
        Organization org,
        IList<Collection> collections,
        IList<OrganizationUser> users,
        IList<Group> groups,
        IEnumerable<CollectionUser> collectionUsers,
        IEnumerable<CollectionGroup> collectionGroups)
    {
        groups.First().OrganizationId = Guid.NewGuid();

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(collections);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(users);

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByManyIds(Arg.Any<IEnumerable<Guid>>())
            .Returns(groups);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.AddAccessAsync(org.Id, collections.Select(c => c.Id).ToList(),
            ToAccessSelection(collectionUsers),
            ToAccessSelection(collectionGroups)
        ));

        Assert.Contains("One or more groups do not belong to the same organization as the collection being assigned.", exception.Message);

        await sutProvider.GetDependency<ICollectionRepository>().ReceivedWithAnyArgs().GetManyByManyIdsAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>().ReceivedWithAnyArgs().GetManyAsync(default);
        await sutProvider.GetDependency<IGroupRepository>().ReceivedWithAnyArgs().GetManyByManyIds(default);
    }

    private static ICollection<CollectionAccessSelection> ToAccessSelection(IEnumerable<CollectionUser> collectionUsers)
    {
        return collectionUsers.Select(cu => new CollectionAccessSelection
        {
            Id = cu.OrganizationUserId,
            Manage = cu.Manage,
            HidePasswords = cu.HidePasswords,
            ReadOnly = cu.ReadOnly
        }).ToList();
    }
    private static ICollection<CollectionAccessSelection> ToAccessSelection(IEnumerable<CollectionGroup> collectionGroups)
    {
        return collectionGroups.Select(cg => new CollectionAccessSelection
        {
            Id = cg.GroupId,
            Manage = cg.Manage,
            HidePasswords = cg.HidePasswords,
            ReadOnly = cg.ReadOnly
        }).ToList();
    }
}
