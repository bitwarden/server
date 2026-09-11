using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;

public class CollectionRepositoryGetManyByOrganizationIdWithAccessTests
{
    [DatabaseTheory, DatabaseData]
    public async Task GetManyByOrganizationIdWithAccessAsync_ReturnsAccessForThisOrganizationOnly(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var user = await userRepository.CreateTestUserAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);
        var group = await groupRepository.CreateTestGroupAsync(organization);

        var collection = new Collection { Name = "Shared", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(collection,
            [new CollectionAccessSelection { Id = group.Id, Manage = true }],
            [new CollectionAccessSelection { Id = orgUser.Id, Manage = true }]);

        // A second organization with its own collection and access, to prove the two are not mixed
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync(identifier: "other");
        var otherUser = await userRepository.CreateTestUserAsync("other");
        var otherOrgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(otherOrganization, otherUser);
        var otherGroup = await groupRepository.CreateTestGroupAsync(otherOrganization);
        var otherCollection = new Collection { Name = "Other", OrganizationId = otherOrganization.Id };
        await collectionRepository.CreateAsync(otherCollection,
            [new CollectionAccessSelection { Id = otherGroup.Id, Manage = true }],
            [new CollectionAccessSelection { Id = otherOrgUser.Id, Manage = true }]);

        // Act
        var results = await collectionRepository.GetManyByOrganizationIdWithAccessAsync(organization.Id);

        // Assert
        var result = Assert.Single(results, r => r.Item1.Id == collection.Id);
        Assert.DoesNotContain(results, r => r.Item1.Id == otherCollection.Id);

        var accessGroup = Assert.Single(result.Item2.Groups);
        Assert.Equal(group.Id, accessGroup.Id);
        Assert.True(accessGroup.Manage);

        var accessUser = Assert.Single(result.Item2.Users);
        Assert.Equal(orgUser.Id, accessUser.Id);
        Assert.True(accessUser.Manage);

        Assert.DoesNotContain(result.Item2.Groups, g => g.Id == otherGroup.Id);
        Assert.DoesNotContain(result.Item2.Users, u => u.Id == otherOrgUser.Id);
    }
}
