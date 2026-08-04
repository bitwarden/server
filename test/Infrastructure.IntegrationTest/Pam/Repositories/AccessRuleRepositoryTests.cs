using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Pam.Repositories;

public class AccessRuleRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task DeleteAsync_WithGovernedCollections_ClearsAssociationsAndKeepsCollections(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        var rule = await accessRuleRepository.CreateAsync(new AccessRule
        {
            OrganizationId = organization.Id,
            Name = "Test Rule",
            Conditions = """{"kind":"human_approval"}""",
        });

        var collection = new Collection
        {
            Name = "Governed Collection",
            OrganizationId = organization.Id,
        };
        await collectionRepository.CreateAsync(collection, [], []);

        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, rule.Id, [collection.Id], []);

        // Sanity check: the collection is governed by the rule before deletion.
        var details = await accessRuleRepository.GetDetailsByIdAsync(rule.Id);
        Assert.NotNull(details);
        Assert.Contains(collection.Id, details.CollectionIds);

        // Act
        await accessRuleRepository.DeleteAsync(rule);

        // Assert: the rule is gone, but the collection survives with its association cleared.
        Assert.Null(await accessRuleRepository.GetByIdAsync(rule.Id));

        var actualCollection = await collectionRepository.GetByIdAsync(collection.Id);
        Assert.NotNull(actualCollection);
        Assert.Null(actualCollection.AccessRuleId);
    }

    /// <summary>
    /// Organization cascades to both Collection and AccessRule, while Collection -> AccessRule is RESTRICT, so
    /// deleting an organization that still has a governed collection depends on those two cascade paths being
    /// applied in the right order. EF's OrganizationRepository.DeleteAsync deletes neither table explicitly — it
    /// relies on the database cascade when the organization row is removed — so this pins that org deletion
    /// survives an active association on every provider.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task OrganizationDeleteAsync_WithGovernedCollection_Succeeds(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        var rule = await accessRuleRepository.CreateAsync(new AccessRule
        {
            OrganizationId = organization.Id,
            Name = "Rule Blocking Org Delete",
            Conditions = """{"kind":"human_approval"}""",
        });

        var collection = new Collection
        {
            Name = "Governed Collection",
            OrganizationId = organization.Id,
        };
        await collectionRepository.CreateAsync(collection, [], []);

        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, rule.Id, [collection.Id], []);

        var governed = await collectionRepository.GetByIdAsync(collection.Id);
        Assert.NotNull(governed);
        Assert.Equal(rule.Id, governed.AccessRuleId);

        // Act
        await organizationRepository.DeleteAsync(organization);

        // Assert: the organization and everything hanging off it is gone.
        Assert.Null(await organizationRepository.GetByIdAsync(organization.Id));
        Assert.Null(await accessRuleRepository.GetByIdAsync(rule.Id));
        Assert.Null(await collectionRepository.GetByIdAsync(collection.Id));
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateAsync_ReusingNameOfDeletedRule_Succeeds(
        IOrganizationRepository organizationRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        var original = await accessRuleRepository.CreateAsync(new AccessRule
        {
            OrganizationId = organization.Id,
            Name = "Reusable Name",
            Conditions = """{"kind":"human_approval"}""",
        });

        // Act: delete the rule, then create a new one reusing its name. A hard delete removes the row, so the unique
        // index on (OrganizationId, Name) no longer reserves the name.
        await accessRuleRepository.DeleteAsync(original);

        var recreated = await accessRuleRepository.CreateAsync(new AccessRule
        {
            OrganizationId = organization.Id,
            Name = "Reusable Name",
            Conditions = """{"kind":"human_approval"}""",
        });

        // Assert: a distinct, live rule owns the name and the original stays gone.
        Assert.NotEqual(original.Id, recreated.Id);
        Assert.Null(await accessRuleRepository.GetByIdAsync(original.Id));

        var live = await accessRuleRepository.GetByIdAsync(recreated.Id);
        Assert.NotNull(live);
        Assert.Equal("Reusable Name", live.Name);
    }
}
