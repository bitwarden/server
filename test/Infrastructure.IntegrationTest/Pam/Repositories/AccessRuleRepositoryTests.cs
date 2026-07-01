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

        await accessRuleRepository.SetCollectionAssociationsAsync(
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

        // Act: soft-delete the rule, then create a new one reusing its name. The unique index on (OrganizationId, Name)
        // is filtered to live rules, so the soft-deleted row no longer reserves the name.
        await accessRuleRepository.DeleteAsync(original);

        var recreated = await accessRuleRepository.CreateAsync(new AccessRule
        {
            OrganizationId = organization.Id,
            Name = "Reusable Name",
            Conditions = """{"kind":"human_approval"}""",
        });

        // Assert: a distinct, live rule owns the name and the soft-deleted original stays gone.
        Assert.NotEqual(original.Id, recreated.Id);
        Assert.Null(await accessRuleRepository.GetByIdAsync(original.Id));

        var live = await accessRuleRepository.GetByIdAsync(recreated.Id);
        Assert.NotNull(live);
        Assert.Equal("Reusable Name", live.Name);
    }
}
