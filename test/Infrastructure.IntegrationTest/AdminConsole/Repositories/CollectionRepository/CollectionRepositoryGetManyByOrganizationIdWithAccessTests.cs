using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using EfCollectionGroup = Bit.Infrastructure.EntityFramework.AdminConsole.Models.CollectionGroup;
using EfCollectionUser = Bit.Infrastructure.EntityFramework.AdminConsole.Models.CollectionUser;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;

public class CollectionRepositoryGetManyByOrganizationIdWithAccessTests
{
    /// <summary>
    /// A collection carries access rows for its own organization plus planted rows pointing at another
    /// organization's group and member. Only the organization's own access may be reported.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task GetManyByOrganizationIdWithAccessAsync_WithCrossOrganizationAccessRows_ReturnsOwnAccessOnly(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository,
        Database database,
        IServiceProvider services)
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

        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync(identifier: "other");
        var otherUser = await userRepository.CreateTestUserAsync("other");
        var otherOrgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(otherOrganization, otherUser);
        var otherGroup = await groupRepository.CreateTestGroupAsync(otherOrganization);

        await PlantCrossOrganizationAccessAsync(services, database, collection.Id, otherOrgUser.Id, otherGroup.Id);

        // Act
        var results = await collectionRepository.GetManyByOrganizationIdWithAccessAsync(organization.Id);

        // Assert
        var result = Assert.Single(results, r => r.Item1.Id == collection.Id);

        // Single() also asserts the planted foreign rows are absent
        var accessGroup = Assert.Single(result.Item2.Groups);
        Assert.Equal(group.Id, accessGroup.Id);
        Assert.True(accessGroup.Manage);

        var accessUser = Assert.Single(result.Item2.Users);
        Assert.Equal(orgUser.Id, accessUser.Id);
        Assert.True(accessUser.Manage);
    }

    /// <summary>
    /// Writes access rows that point at another organization's member and group. Every repository write path
    /// refuses to create these — that is what this fix and its siblings enforce — so they are written straight
    /// to the tables: raw SQL for the Dapper/SqlServer pair, the EF <see cref="DatabaseContext"/> elsewhere.
    /// </summary>
    private static async Task PlantCrossOrganizationAccessAsync(
        IServiceProvider services,
        Database database,
        Guid collectionId,
        Guid organizationUserId,
        Guid groupId)
    {
        if (database.Type == SupportedDatabaseProviders.SqlServer && !database.UseEf)
        {
            await using var connection = new SqlConnection(database.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO [dbo].[CollectionUser] ([CollectionId], [OrganizationUserId], [ReadOnly], [HidePasswords], [Manage])
                VALUES (@CollectionId, @OrganizationUserId, 0, 0, 1);
                INSERT INTO [dbo].[CollectionGroup] ([CollectionId], [GroupId], [ReadOnly], [HidePasswords], [Manage])
                VALUES (@CollectionId, @GroupId, 0, 0, 1);
                """;
            command.Parameters.AddWithValue("@CollectionId", collectionId);
            command.Parameters.AddWithValue("@OrganizationUserId", organizationUserId);
            command.Parameters.AddWithValue("@GroupId", groupId);
            await command.ExecuteNonQueryAsync();
            return;
        }

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        dbContext.CollectionUsers.Add(new EfCollectionUser
        {
            CollectionId = collectionId,
            OrganizationUserId = organizationUserId,
            Manage = true
        });
        dbContext.CollectionGroups.Add(new EfCollectionGroup
        {
            CollectionId = collectionId,
            GroupId = groupId,
            Manage = true
        });
        await dbContext.SaveChangesAsync();
    }
}
