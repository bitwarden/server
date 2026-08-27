using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;

/// <summary>
/// Covers <see cref="CollectionDetails.HasEnabledAccessRule"/> on the three collection read paths that feed a
/// client: the sync read, the organization listing behind the Admin Console vault, and the single-collection read.
///
/// The flag is derived rather than stored — <see cref="Collection.AccessRuleId"/> records the association, but a
/// rule that is switched off gates nothing, so each read joins the rule and reports whether it is enabled. MSSQL
/// computes this in the stored procedures and Entity Framework reimplements it in LINQ, so every test here runs the
/// same truth table against both: ungoverned, governed by an enabled rule, governed by a disabled rule.
/// </summary>
public class CollectionRepositoryHasEnabledAccessRuleTests
{
    private const string _conditions = """{"kind":"human_approval"}""";

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByUserIdAsync_ReportsWhetherTheGoverningRuleIsEnabled(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var (organization, orgUser, user) = await CreateMemberAsync(
            userRepository, organizationRepository, organizationUserRepository);
        var (ungoverned, enabled, disabled) = await CreateThreeCollectionsAsync(
            collectionRepository, accessRuleRepository, organization, orgUser);

        // Act
        var actual = await collectionRepository.GetManyByUserIdAsync(user.Id);

        // Assert
        Assert.False(Single(actual, ungoverned.Id).HasEnabledAccessRule);
        Assert.True(Single(actual, enabled.Id).HasEnabledAccessRule);
        Assert.False(Single(actual, disabled.Id).HasEnabledAccessRule);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManySharedByOrganizationIdWithPermissionsAsync_ReportsWhetherTheGoverningRuleIsEnabled(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var (organization, orgUser, user) = await CreateMemberAsync(
            userRepository, organizationRepository, organizationUserRepository);
        var (ungoverned, enabled, disabled) = await CreateThreeCollectionsAsync(
            collectionRepository, accessRuleRepository, organization, orgUser);

        // Act
        var actual = await collectionRepository.GetManySharedByOrganizationIdWithPermissionsAsync(
            organization.Id, user.Id, false);

        // Assert
        Assert.False(Single(actual, ungoverned.Id).HasEnabledAccessRule);
        Assert.True(Single(actual, enabled.Id).HasEnabledAccessRule);
        Assert.False(Single(actual, disabled.Id).HasEnabledAccessRule);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetByIdWithPermissionsAsync_ReportsWhetherTheGoverningRuleIsEnabled(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var (organization, orgUser, user) = await CreateMemberAsync(
            userRepository, organizationRepository, organizationUserRepository);
        var (ungoverned, enabled, disabled) = await CreateThreeCollectionsAsync(
            collectionRepository, accessRuleRepository, organization, orgUser);

        // Act + Assert
        Assert.False((await ReadAsync(ungoverned)).HasEnabledAccessRule);
        Assert.True((await ReadAsync(enabled)).HasEnabledAccessRule);
        Assert.False((await ReadAsync(disabled)).HasEnabledAccessRule);

        async Task<CollectionAdminDetails> ReadAsync(Collection collection)
        {
            var actual = await collectionRepository.GetByIdWithPermissionsAsync(collection.Id, user.Id, false);
            Assert.NotNull(actual);
            return actual;
        }
    }

    /// <summary>
    /// Switching a rule off ungoverns its collections for badge purposes without touching the association, so the
    /// flag has to follow the rule's state rather than the collection's row.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task GetManyByUserIdAsync_AfterTheRuleIsDisabled_StopsReportingTheCollectionAsGoverned(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var (organization, orgUser, user) = await CreateMemberAsync(
            userRepository, organizationRepository, organizationUserRepository);
        var rule = await CreateRuleAsync(accessRuleRepository, organization.Id, "Toggled", enabled: true);
        var collection = await CreateAssignedCollectionAsync(collectionRepository, organization, orgUser);
        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, rule.Id, [collection.Id], []);

        var before = await collectionRepository.GetManyByUserIdAsync(user.Id);
        Assert.True(Single(before, collection.Id).HasEnabledAccessRule);

        // Act
        rule.Enabled = false;
        await accessRuleRepository.ReplaceAsync(rule);

        // Assert: still associated, no longer gating.
        var after = await collectionRepository.GetManyByUserIdAsync(user.Id);
        Assert.False(Single(after, collection.Id).HasEnabledAccessRule);

        var stored = await collectionRepository.GetByIdAsync(collection.Id);
        Assert.NotNull(stored);
        Assert.Equal(rule.Id, stored.AccessRuleId);
    }

    private static T Single<T>(IEnumerable<T> collections, Guid collectionId) where T : Collection
        => Assert.Single(collections, c => c.Id == collectionId);

    private static async Task<(Organization, OrganizationUser, User)> CreateMemberAsync(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);
        return (organization, orgUser, user);
    }

    private static async Task<(Collection Ungoverned, Collection Enabled, Collection Disabled)>
        CreateThreeCollectionsAsync(
            ICollectionRepository collectionRepository,
            IAccessRuleRepository accessRuleRepository,
            Organization organization,
            OrganizationUser orgUser)
    {
        var enabledRule = await CreateRuleAsync(accessRuleRepository, organization.Id, "Enabled", enabled: true);
        var disabledRule = await CreateRuleAsync(accessRuleRepository, organization.Id, "Disabled", enabled: false);

        var ungoverned = await CreateAssignedCollectionAsync(collectionRepository, organization, orgUser);
        var governedByEnabled = await CreateAssignedCollectionAsync(collectionRepository, organization, orgUser);
        var governedByDisabled = await CreateAssignedCollectionAsync(collectionRepository, organization, orgUser);

        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, enabledRule.Id, [governedByEnabled.Id], []);
        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, disabledRule.Id, [governedByDisabled.Id], []);

        return (ungoverned, governedByEnabled, governedByDisabled);
    }

    /// <summary>
    /// The collection must be assigned to the member, otherwise the user-scoped read paths do not return it at all.
    /// </summary>
    private static async Task<Collection> CreateAssignedCollectionAsync(
        ICollectionRepository collectionRepository,
        Organization organization,
        OrganizationUser orgUser)
    {
        var collection = new Collection
        {
            OrganizationId = organization.Id,
            Name = $"Test Collection {Guid.NewGuid()}"
        };

        await collectionRepository.CreateAsync(collection, groups: [], users:
        [
            new CollectionAccessSelection { Id = orgUser.Id, ReadOnly = false, HidePasswords = false, Manage = true }
        ]);

        return collection;
    }

    private static Task<AccessRule> CreateRuleAsync(
        IAccessRuleRepository accessRuleRepository, Guid organizationId, string name, bool enabled)
        => accessRuleRepository.CreateAsync(new AccessRule
        {
            OrganizationId = organizationId,
            Name = $"{name} {Guid.NewGuid()}",
            Conditions = _conditions,
            Enabled = enabled,
        });
}
