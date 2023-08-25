using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationCollections;
using Bit.Core.Repositories;
using Bit.Core.Services;
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
    public async Task AddAccessAsync_Success(SutProvider<BulkAddCollectionAccessCommand> sutProvider,
        Organization org,
        ICollection<Collection> collections,
        ICollection<OrganizationUser> users,
        ICollection<Group> groups,
        IEnumerable<CollectionUser> collectionUsers,
        IEnumerable<CollectionGroup> collectionGroups)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(users);

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByManyIds(Arg.Any<IEnumerable<Guid>>())
            .Returns(groups);

        var userAccessSelections = ToAccessSelection(collectionUsers);
        var groupAccessSelections = ToAccessSelection(collectionGroups);
        await sutProvider.Sut.AddAccessAsync(org.Id, collections,
            userAccessSelections,
            groupAccessSelections
        );

        await sutProvider.GetDependency<IOrganizationUserRepository>().ReceivedWithAnyArgs().GetManyAsync(default);
        await sutProvider.GetDependency<IGroupRepository>().ReceivedWithAnyArgs().GetManyByManyIds(default);

        await sutProvider.GetDependency<ICollectionRepository>().Received().CreateOrUpdateAccessForManyAsync(
            org.Id,
            Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(collections.Select(c => c.Id))),
            userAccessSelections,
            groupAccessSelections);

        await sutProvider.GetDependency<IEventService>().Received().LogCollectionEventsAsync(
            Arg.Is<IEnumerable<(Collection, EventType, DateTime?)>>(
                events => events.All(e =>
                    collections.Contains(e.Item1) &&
                    e.Item2 == EventType.Collection_Updated &&
                    e.Item3.HasValue
                )
            )
        );
    }


    [Theory, BitAutoData, CollectionCustomization]
    public async Task ValidateRequestAsync_NoCollection_Failure(SutProvider<BulkAddCollectionAccessCommand> sutProvider,
        Organization org,
        IEnumerable<CollectionUser> collectionUsers,
        IEnumerable<CollectionGroup> collectionGroups)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.AddAccessAsync(org.Id, Enumerable.Empty<Collection>().ToList(),
            ToAccessSelection(collectionUsers),
            ToAccessSelection(collectionGroups)
        ));

        Assert.Contains("No collections were provided.", exception.Message);

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs().GetManyAsync(default);
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().GetManyByManyIds(default);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task ValidateRequestAsync_DifferentOrgs_Failure(SutProvider<BulkAddCollectionAccessCommand> sutProvider,
        Organization org,
        ICollection<Collection> collections,
        IEnumerable<CollectionUser> collectionUsers,
        IEnumerable<CollectionGroup> collectionGroups)
    {
        collections.First().OrganizationId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.AddAccessAsync(org.Id, collections,
            ToAccessSelection(collectionUsers),
            ToAccessSelection(collectionGroups)
        ));

        Assert.Contains("All collections must belong to the same organization.", exception.Message);

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

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(users);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.AddAccessAsync(org.Id, collections,
            ToAccessSelection(collectionUsers),
            ToAccessSelection(collectionGroups)
        ));

        Assert.Contains("One or more users do not exist.", exception.Message);

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

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(users);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.AddAccessAsync(org.Id, collections,
            ToAccessSelection(collectionUsers),
            ToAccessSelection(collectionGroups)
        ));

        Assert.Contains("One or more users do not belong to the same organization as the collection being assigned.", exception.Message);

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

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(users);

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByManyIds(Arg.Any<IEnumerable<Guid>>())
            .Returns(groups);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.AddAccessAsync(org.Id, collections,
            ToAccessSelection(collectionUsers),
            ToAccessSelection(collectionGroups)
        ));

        Assert.Contains("One or more groups do not exist.", exception.Message);

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

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(users);

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByManyIds(Arg.Any<IEnumerable<Guid>>())
            .Returns(groups);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.AddAccessAsync(org.Id, collections,
            ToAccessSelection(collectionUsers),
            ToAccessSelection(collectionGroups)
        ));

        Assert.Contains("One or more groups do not belong to the same organization as the collection being assigned.", exception.Message);

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
