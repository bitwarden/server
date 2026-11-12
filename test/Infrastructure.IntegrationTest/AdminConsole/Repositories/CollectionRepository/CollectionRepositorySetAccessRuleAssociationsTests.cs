using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Infrastructure.IntegrationTest.Comparers;
using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;

/// <summary>
/// Covers <see cref="ICollectionRepository.SetAccessRuleAssociationsAsync"/>, which points collections at a PAM
/// access rule and detaches the ones that should no longer reference it. The MSSQL implementation wraps
/// <c>Collection_SetAccessRuleAssociations</c> and the EF implementation reimplements it, so each test here pins a
/// clause that both have to agree on.
/// </summary>
public class CollectionRepositorySetAccessRuleAssociationsTests
{
    private const string _conditions = """{"kind":"human_approval"}""";

    [DatabaseTheory, DatabaseData]
    public async Task SetAccessRuleAssociationsAsync_WithCollectionsToAssign_PointsThemAtTheRule(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var rule = await CreateRuleAsync(accessRuleRepository, organization.Id, "Assign");

        // Backdated so the bumped RevisionDate is always strictly later, even on a run fast enough to land in the
        // same datetime tick.
        var collection = await collectionRepository.CreateAsync(new Collection
        {
            OrganizationId = organization.Id,
            Name = $"test {Guid.NewGuid()}",
            RevisionDate = DateTime.UtcNow.AddMinutes(-10),
        });
        var before = await collectionRepository.GetByIdAsync(collection.Id);
        Assert.NotNull(before);
        Assert.Null(before.AccessRuleId);

        // Act
        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, rule.Id, [collection.Id], []);

        // Assert
        var actual = await collectionRepository.GetByIdAsync(collection.Id);
        Assert.NotNull(actual);
        Assert.Equal(rule.Id, actual.AccessRuleId);
        Assert.True(actual.RevisionDate > before.RevisionDate,
            "The RevisionDate is expected to be bumped when the association changes.");
    }

    [DatabaseTheory, DatabaseData]
    public async Task SetAccessRuleAssociationsAsync_WithCollectionsToClear_DetachesThem(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var rule = await CreateRuleAsync(accessRuleRepository, organization.Id, "Clear");

        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, rule.Id, [collection.Id], []);

        // Act
        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, rule.Id, [], [collection.Id]);

        // Assert: the collection survives, ungoverned.
        var actual = await collectionRepository.GetByIdAsync(collection.Id);
        Assert.NotNull(actual);
        Assert.Null(actual.AccessRuleId);
    }

    /// <summary>
    /// The clear pass is scoped to the rule being written — both implementations qualify it with
    /// <c>AccessRuleId = @AccessRuleId</c>. A caller that names a collection governed by some other rule must not
    /// detach it, otherwise one rule's update could silently ungovern another rule's collections.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task SetAccessRuleAssociationsAsync_ClearingCollectionGovernedByAnotherRule_LeavesItAlone(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var ruleA = await CreateRuleAsync(accessRuleRepository, organization.Id, "Rule A");
        var ruleB = await CreateRuleAsync(accessRuleRepository, organization.Id, "Rule B");

        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, ruleB.Id, [collection.Id], []);

        // Act: rule A tries to clear a collection that rule B governs.
        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, ruleA.Id, [], [collection.Id]);

        // Assert
        var actual = await collectionRepository.GetByIdAsync(collection.Id);
        Assert.NotNull(actual);
        Assert.Equal(ruleB.Id, actual.AccessRuleId);
    }

    /// <summary>
    /// Both passes are scoped to the organization, so a collection ID from a different organization is inert even
    /// though the caller named it.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task SetAccessRuleAssociationsAsync_WithCollectionInAnotherOrganization_DoesNotAssignIt(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync(identifier: "owner");
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync(identifier: "other");
        var rule = await CreateRuleAsync(accessRuleRepository, organization.Id, "Cross-org");

        var ownCollection = await collectionRepository.CreateTestCollectionAsync(organization);
        var foreignCollection = await collectionRepository.CreateTestCollectionAsync(otherOrganization);

        // Act
        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, rule.Id, [ownCollection.Id, foreignCollection.Id], []);

        // Assert
        var actualOwn = await collectionRepository.GetByIdAsync(ownCollection.Id);
        Assert.NotNull(actualOwn);
        Assert.Equal(rule.Id, actualOwn.AccessRuleId);

        var actualForeign = await collectionRepository.GetByIdAsync(foreignCollection.Id);
        Assert.NotNull(actualForeign);
        Assert.Null(actualForeign.AccessRuleId);
    }

    /// <summary>
    /// Both implementations clear before they assign, so a collection named in both lists ends up assigned. This is
    /// the contract callers depend on when they compute the two sets from an overlapping "desired state" diff.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task SetAccessRuleAssociationsAsync_WithCollectionInBothLists_LeavesItAssigned(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var rule = await CreateRuleAsync(accessRuleRepository, organization.Id, "Both lists");

        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, rule.Id, [collection.Id], []);

        // Act
        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, rule.Id, [collection.Id], [collection.Id]);

        // Assert
        var actual = await collectionRepository.GetByIdAsync(collection.Id);
        Assert.NotNull(actual);
        Assert.Equal(rule.Id, actual.AccessRuleId);
    }

    /// <summary>
    /// Assign is unqualified by the current rule, so moving a collection between rules needs no matching clear from
    /// the rule that governs it today.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task SetAccessRuleAssociationsAsync_AssigningCollectionGovernedByAnotherRule_ReassignsIt(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var ruleA = await CreateRuleAsync(accessRuleRepository, organization.Id, "Rule A");
        var ruleB = await CreateRuleAsync(accessRuleRepository, organization.Id, "Rule B");

        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, ruleA.Id, [collection.Id], []);

        // Act
        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, ruleB.Id, [collection.Id], []);

        // Assert
        var actual = await collectionRepository.GetByIdAsync(collection.Id);
        Assert.NotNull(actual);
        Assert.Equal(ruleB.Id, actual.AccessRuleId);

        // The rule that used to govern it no longer does.
        var detailsA = await accessRuleRepository.GetDetailsByIdAsync(ruleA.Id);
        Assert.NotNull(detailsA);
        Assert.DoesNotContain(collection.Id, detailsA.CollectionIds);
    }

    /// <summary>
    /// Assigning a collection to an enabled rule gates it, so its members resync. A member who cannot see the
    /// collection sees nothing change.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task SetAccessRuleAssociationsAsync_AssigningToEnabledRule_BumpsOnlyMembersOfAssignedCollection(
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IUserRepository userRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var (member, memberOrgUser) =
            await userRepository.CreateConfirmedMemberAsync(organizationUserRepository, organization);
        var (outsider, outsiderOrgUser) =
            await userRepository.CreateConfirmedMemberAsync(organizationUserRepository, organization);

        var rule = await CreateRuleAsync(accessRuleRepository, organization.Id, "Enabled");
        var collection = await collectionRepository.CreateCollectionWithMembersAsync(organization, memberOrgUser);
        await collectionRepository.CreateCollectionWithMembersAsync(organization, outsiderOrgUser);
        await userRepository.BackdateAccountRevisionDatesAsync(member, outsider);

        // Act
        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, rule.Id, [collection.Id], []);

        // Assert
        await userRepository.AssertBumpedAsync(member);
        await userRepository.AssertNotBumpedAsync(outsider);
    }

    /// <summary>
    /// A disabled rule gates nothing, so assigning a collection to it is invisible to the collection's members.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task SetAccessRuleAssociationsAsync_AssigningToDisabledRule_BumpsNoOne(
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IUserRepository userRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var (member, memberOrgUser) =
            await userRepository.CreateConfirmedMemberAsync(organizationUserRepository, organization);

        var rule = await CreateRuleAsync(accessRuleRepository, organization.Id, "Disabled", enabled: false);
        var collection = await collectionRepository.CreateCollectionWithMembersAsync(organization, memberOrgUser);
        await userRepository.BackdateAccountRevisionDatesAsync(member);

        // Act
        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, rule.Id, [collection.Id], []);

        // Assert
        await userRepository.AssertNotBumpedAsync(member);
    }

    /// <summary>
    /// Clearing a collection from an enabled rule ungates it, so its members resync while the members of the rule's
    /// other collections see nothing change.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task SetAccessRuleAssociationsAsync_ClearingFromEnabledRule_BumpsOnlyMembersOfClearedCollection(
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IUserRepository userRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var (clearedMember, clearedOrgUser) =
            await userRepository.CreateConfirmedMemberAsync(organizationUserRepository, organization);
        var (keptMember, keptOrgUser) =
            await userRepository.CreateConfirmedMemberAsync(organizationUserRepository, organization);

        var rule = await CreateRuleAsync(accessRuleRepository, organization.Id, "Clear");
        var cleared = await collectionRepository.CreateCollectionWithMembersAsync(organization, clearedOrgUser);
        var kept = await collectionRepository.CreateCollectionWithMembersAsync(organization, keptOrgUser);
        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, rule.Id, [cleared.Id, kept.Id], []);
        await userRepository.BackdateAccountRevisionDatesAsync(clearedMember, keptMember);

        // Act
        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, rule.Id, [kept.Id], [cleared.Id]);

        // Assert
        await userRepository.AssertBumpedAsync(clearedMember);
        await userRepository.AssertNotBumpedAsync(keptMember);
    }

    /// <summary>
    /// The update command passes every collection the rule should govern, including ones it already does. Those are
    /// left untouched, so neither their RevisionDate nor their members' account revision date moves, and a rename
    /// does not make the organization resync.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task SetAccessRuleAssociationsAsync_ReassigningAlreadyGovernedCollection_ChangesNothing(
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IUserRepository userRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var (member, memberOrgUser) =
            await userRepository.CreateConfirmedMemberAsync(organizationUserRepository, organization);

        var rule = await CreateRuleAsync(accessRuleRepository, organization.Id, "Unchanged");
        var collection = await collectionRepository.CreateCollectionWithMembersAsync(organization, memberOrgUser);
        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, rule.Id, [collection.Id], []);
        var before = await collectionRepository.GetByIdAsync(collection.Id);
        Assert.NotNull(before);
        await userRepository.BackdateAccountRevisionDatesAsync(member);

        // Act
        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, rule.Id, [collection.Id], []);

        // Assert
        await userRepository.AssertNotBumpedAsync(member);
        var actual = await collectionRepository.GetByIdAsync(collection.Id);
        Assert.NotNull(actual);
        Assert.Equal(before.RevisionDate, actual.RevisionDate, LaxDateTimeComparer.Default);
    }

    /// <summary>
    /// Moving a collection from an enabled rule to a disabled one ungates it even though it stays governed, so its
    /// members resync.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task SetAccessRuleAssociationsAsync_MovingFromEnabledToDisabledRule_BumpsMembers(
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IUserRepository userRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var (member, memberOrgUser) =
            await userRepository.CreateConfirmedMemberAsync(organizationUserRepository, organization);

        var enabledRule = await CreateRuleAsync(accessRuleRepository, organization.Id, "Enabled");
        var disabledRule = await CreateRuleAsync(accessRuleRepository, organization.Id, "Disabled", enabled: false);
        var collection = await collectionRepository.CreateCollectionWithMembersAsync(organization, memberOrgUser);
        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, enabledRule.Id, [collection.Id], []);
        await userRepository.BackdateAccountRevisionDatesAsync(member);

        // Act
        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, disabledRule.Id, [collection.Id], []);

        // Assert
        await userRepository.AssertBumpedAsync(member);
    }

    /// <summary>
    /// The commands call this unconditionally, including when a rule is saved with no collection changes at all, so
    /// two empty sets have to be a safe no-op rather than an error.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task SetAccessRuleAssociationsAsync_WithNoCollections_LeavesExistingAssociationsIntact(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var rule = await CreateRuleAsync(accessRuleRepository, organization.Id, "No-op");

        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, rule.Id, [collection.Id], []);

        // Act
        await collectionRepository.SetAccessRuleAssociationsAsync(organization.Id, rule.Id, [], []);

        // Assert
        var actual = await collectionRepository.GetByIdAsync(collection.Id);
        Assert.NotNull(actual);
        Assert.Equal(rule.Id, actual.AccessRuleId);
    }

    /// <summary>
    /// The counterpart to the foreign-collection case: the rule has to belong to the organization too. The foreign
    /// key only proves the rule exists, so without an explicit tenancy check a caller could govern its own
    /// collections with another organization's rule — handing that organization control of the conditions gating
    /// access to data it cannot see. Both implementations refuse the assignment rather than erroring, matching how a
    /// collection from another organization is already inert.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task SetAccessRuleAssociationsAsync_WithRuleFromAnotherOrganization_DoesNotAssignIt(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync(identifier: "owner");
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync(identifier: "other");
        var foreignRule = await CreateRuleAsync(accessRuleRepository, otherOrganization.Id, "Foreign rule");

        var collection = await collectionRepository.CreateTestCollectionAsync(organization);

        // Act
        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, foreignRule.Id, [collection.Id], []);

        // Assert
        var actual = await collectionRepository.GetByIdAsync(collection.Id);
        Assert.NotNull(actual);
        Assert.Null(actual.AccessRuleId);
    }

    /// <summary>
    /// A foreign rule must not take the existing association down with it: the clear pass is scoped to the rule
    /// being written, so a collection governed by its own organization's rule is left alone.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task SetAccessRuleAssociationsAsync_WithRuleFromAnotherOrganization_LeavesExistingAssociationIntact(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync(identifier: "owner");
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync(identifier: "other");
        var ownRule = await CreateRuleAsync(accessRuleRepository, organization.Id, "Own");
        var foreignRule = await CreateRuleAsync(accessRuleRepository, otherOrganization.Id, "Foreign");

        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, ownRule.Id, [collection.Id], []);

        // Act
        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, foreignRule.Id, [collection.Id], [collection.Id]);

        // Assert
        var actual = await collectionRepository.GetByIdAsync(collection.Id);
        Assert.NotNull(actual);
        Assert.Equal(ownRule.Id, actual.AccessRuleId);
    }

    private static Task<AccessRule> CreateRuleAsync(
        IAccessRuleRepository accessRuleRepository, Guid organizationId, string name, bool enabled = true)
        => accessRuleRepository.CreateAsync(new AccessRule
        {
            OrganizationId = organizationId,
            Name = $"{name} {Guid.NewGuid()}",
            Conditions = _conditions,
            Enabled = enabled,
        });
}
