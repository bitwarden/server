using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;

/// <summary>
/// Pins who <c>User_BumpAccountRevisionDateByCollectionIds</c> and its EF mirror bump, through
/// <see cref="ICollectionRepository.CreateOrUpdateAccessForManyAsync"/>, which calls it for the collections it is given.
/// </summary>
public class CollectionRepositoryBumpAccountRevisionDateTests
{
    [DatabaseTheory, DatabaseData]
    public async Task CreateOrUpdateAccessForManyAsync_BumpsOnlyConfirmedMembersWhoReachTheCollections(
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        IUserRepository userRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var (direct, directOrgUser) =
            await userRepository.CreateConfirmedMemberAsync(organizationUserRepository, organization);
        var (grouped, groupedOrgUser) =
            await userRepository.CreateConfirmedMemberAsync(organizationUserRepository, organization);
        var (both, bothOrgUser) =
            await userRepository.CreateConfirmedMemberAsync(organizationUserRepository, organization);
        var (elsewhere, elsewhereOrgUser) =
            await userRepository.CreateConfirmedMemberAsync(organizationUserRepository, organization);
        var unconfirmed = await userRepository.CreateTestUserAsync();
        var unconfirmedOrgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = unconfirmed.Id,
            Status = OrganizationUserStatusType.Accepted,
            Type = OrganizationUserType.User,
        });

        var group = await groupRepository.CreateTestGroupAsync(organization);
        await groupRepository.UpdateUsersAsync(group.Id, [groupedOrgUser.Id, bothOrgUser.Id], DateTime.UtcNow);

        var target = new Collection { OrganizationId = organization.Id, Name = $"target {Guid.NewGuid()}" };
        await collectionRepository.CreateAsync(target,
            [new CollectionAccessSelection { Id = group.Id }],
            [
                new CollectionAccessSelection { Id = directOrgUser.Id },
                new CollectionAccessSelection { Id = bothOrgUser.Id },
                new CollectionAccessSelection { Id = unconfirmedOrgUser.Id },
            ]);
        await collectionRepository.CreateCollectionWithMembersAsync(organization, elsewhereOrgUser);

        await userRepository.BackdateAccountRevisionDatesAsync(direct, grouped, both, elsewhere, unconfirmed);

        // Act
        await collectionRepository.CreateOrUpdateAccessForManyAsync(organization.Id, [target.Id], [], [],
            DateTime.UtcNow);

        // Assert
        await userRepository.AssertBumpedAsync(direct);
        await userRepository.AssertBumpedAsync(grouped);
        await userRepository.AssertBumpedAsync(both);
        await userRepository.AssertNotBumpedAsync(elsewhere);
        await userRepository.AssertNotBumpedAsync(unconfirmed);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateOrUpdateAccessForManyAsync_DoesNotBumpMembersOfAnotherOrganization(
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IUserRepository userRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync(identifier: "owner");
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync(identifier: "other");
        var (member, memberOrgUser) =
            await userRepository.CreateConfirmedMemberAsync(organizationUserRepository, organization);
        var (outsider, outsiderOrgUser) =
            await userRepository.CreateConfirmedMemberAsync(organizationUserRepository, otherOrganization);

        var collection = await collectionRepository.CreateCollectionWithMembersAsync(organization, memberOrgUser);
        var foreignCollection =
            await collectionRepository.CreateCollectionWithMembersAsync(otherOrganization, outsiderOrgUser);
        await userRepository.BackdateAccountRevisionDatesAsync(member, outsider);

        // Act: the foreign collection is named, but the bump is scoped to the organization.
        await collectionRepository.CreateOrUpdateAccessForManyAsync(organization.Id,
            [collection.Id, foreignCollection.Id], [], [], DateTime.UtcNow);

        // Assert
        await userRepository.AssertBumpedAsync(member);
        await userRepository.AssertNotBumpedAsync(outsider);
    }
}
