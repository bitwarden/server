using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Pam.Repositories;

public class AccessRuleRepositoryTests
{
    private const string _conditions = """{"kind":"human_approval"}""";

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
    /// A request pins its governing rule in AccessRequest.RuleId, and FK_AccessRequest_AccessRule does not cascade
    /// (NO ACTION on SQL Server, RESTRICT on the EF providers), so deleting a rule any request has pinned fails
    /// outright unless the delete path detaches those requests first.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task DeleteAsync_WithPinnedRequests_DetachesRequestsAndDeletesRule(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var rule = await CreateRuleAsync(accessRuleRepository, organization.Id, "Pinned Rule");
        var now = DateTime.UtcNow;

        var request = await accessRequestRepository.CreateAsync(new AccessRequest
        {
            OrganizationId = organization.Id,
            CollectionId = collection.Id,
            CipherId = Guid.NewGuid(),
            RequesterId = Guid.NewGuid(),
            NotBefore = now,
            NotAfter = now.AddHours(1),
            Status = AccessRequestStatus.Pending,
            CreationDate = now,
            RuleId = rule.Id,
        });
        Assert.Equal(rule.Id, (await accessRequestRepository.GetByIdAsync(request.Id))!.RuleId);

        // Act
        await accessRuleRepository.DeleteAsync(rule);

        // Assert: the rule is gone and the request survives, detached rather than deleted -- its window and
        // decision log remain the record of what was granted.
        Assert.Null(await accessRuleRepository.GetByIdAsync(rule.Id));

        var persisted = await accessRequestRepository.GetByIdAsync(request.Id);
        Assert.NotNull(persisted);
        Assert.Null(persisted!.RuleId);
        Assert.Equal(AccessRequestStatus.Pending, persisted.Status);
    }

    /// <summary>
    /// Organization cascades to both AccessRequest and AccessLease, while the two reference each other under
    /// RESTRICT (AccessRequest.ExtensionOfLeaseId and AccessLease.AccessRequestId) and requests additionally pin a
    /// rule. Whichever cascade a provider fires first is blocked by the other, so this pins that deleting an
    /// organization holding an extended lease succeeds everywhere rather than depending on cascade order.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task OrganizationDeleteAsync_WithExtendedLease_Succeeds(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var rule = await CreateRuleAsync(accessRuleRepository, organization.Id, "Rule For Extended Lease");
        var now = DateTime.UtcNow;
        var requesterId = Guid.NewGuid();
        var cipherId = Guid.NewGuid();

        // An approved request that pins the rule, activated into a lease...
        var request = await accessRequestRepository.CreateAsync(new AccessRequest
        {
            OrganizationId = organization.Id,
            CollectionId = collection.Id,
            CipherId = cipherId,
            RequesterId = requesterId,
            NotBefore = now.AddMinutes(-5),
            NotAfter = now.AddHours(1),
            Status = AccessRequestStatus.Approved,
            CreationDate = now,
            RuleId = rule.Id,
        });

        var lease = new AccessLease
        {
            Id = CombGuid.Generate(),
            AccessRequestId = request.Id,
            OrganizationId = organization.Id,
            CollectionId = collection.Id,
            CipherId = cipherId,
            RequesterId = requesterId,
            Status = AccessLeaseStatus.Active,
            NotBefore = request.NotBefore,
            NotAfter = request.NotAfter,
            CreationDate = now,
        };
        Assert.Equal(AccessLeaseMintOutcome.Minted,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(lease, now, false));

        // ...and an extension request pointing back at that lease, closing the reference cycle.
        var extension = new AccessRequest
        {
            Id = CombGuid.Generate(),
            ExtensionOfLeaseId = lease.Id,
            OrganizationId = organization.Id,
            CollectionId = collection.Id,
            CipherId = cipherId,
            RequesterId = requesterId,
            NotBefore = lease.NotAfter,
            NotAfter = lease.NotAfter.AddHours(1),
            Status = AccessRequestStatus.Approved,
            CreationDate = now,
            RuleId = rule.Id,
        };
        var extensionDecision = new AccessDecision
        {
            Id = CombGuid.Generate(),
            AccessRequestId = extension.Id,
            DeciderKind = AccessDeciderKind.Automatic,
            Verdict = AccessDecisionVerdict.Approve,
            CreationDate = now,
        };
        Assert.Equal(AccessLeaseExtendOutcome.Extended,
            await accessRequestRepository.CreateApprovedExtensionAsync(extension, extensionDecision, now));

        // Act
        await organizationRepository.DeleteAsync(organization);

        // Assert: the organization and every PAM row hanging off it is gone.
        Assert.Null(await organizationRepository.GetByIdAsync(organization.Id));
        Assert.Null(await accessRuleRepository.GetByIdAsync(rule.Id));
        Assert.Null(await accessLeaseRepository.GetByIdAsync(lease.Id));
        Assert.Null(await accessRequestRepository.GetByIdAsync(request.Id));
        Assert.Null(await accessRequestRepository.GetByIdAsync(extension.Id));
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

    /// <summary>
    /// The read is org-scoped: MSSQL filters in <c>AccessRule_ReadByOrganizationId</c> and EF filters in the query,
    /// so a rule belonging to another organization must never appear. Leaking one would expose the conditions
    /// gating access to data the caller cannot see.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task GetManyByOrganizationIdAsync_ReturnsOnlyTheOrganizationsRules(
        IOrganizationRepository organizationRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync(identifier: "owner");
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync(identifier: "other");

        var first = await CreateRuleAsync(accessRuleRepository, organization.Id, "First");
        var second = await CreateRuleAsync(accessRuleRepository, organization.Id, "Second");
        var foreign = await CreateRuleAsync(accessRuleRepository, otherOrganization.Id, "Foreign");

        // Act
        var actual = await accessRuleRepository.GetManyByOrganizationIdAsync(organization.Id);

        // Assert
        Assert.Equal(new[] { first.Id, second.Id }.Order(), actual.Select(r => r.Id).Order());
        Assert.DoesNotContain(foreign.Id, actual.Select(r => r.Id));
    }

    /// <summary>
    /// Every column has to survive the round trip. The two stacks hydrate differently -- Dapper maps sproc columns
    /// onto <see cref="AccessRuleDetails"/> directly while EF maps the entity through AutoMapper -- so a column
    /// missing from one side is a silent divergence rather than a failure.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task GetManyByOrganizationIdAsync_RoundTripsEveryField(
        IOrganizationRepository organizationRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var lastEditedBy = Guid.NewGuid();

        var expected = await accessRuleRepository.CreateAsync(new AccessRule
        {
            OrganizationId = organization.Id,
            Name = "Fully Populated",
            Description = "Every field set to a non-default value.",
            Conditions = _conditions,
            SingleActiveLease = true,
            DefaultLeaseDurationSeconds = 900,
            MaxLeaseDurationSeconds = 3600,
            Enabled = false,
            AllowsExtensions = true,
            MaxExtensionDurationSeconds = 1800,
            LastEditedBy = lastEditedBy,
        });

        // Act
        var actual = Assert.Single(await accessRuleRepository.GetManyByOrganizationIdAsync(organization.Id));

        // Assert
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(organization.Id, actual.OrganizationId);
        Assert.Equal("Fully Populated", actual.Name);
        Assert.Equal("Every field set to a non-default value.", actual.Description);
        Assert.Equal(_conditions, actual.Conditions);
        Assert.True(actual.SingleActiveLease);
        Assert.Equal(900, actual.DefaultLeaseDurationSeconds);
        Assert.Equal(3600, actual.MaxLeaseDurationSeconds);
        Assert.False(actual.Enabled);
        Assert.True(actual.AllowsExtensions);
        Assert.Equal(1800, actual.MaxExtensionDurationSeconds);
        Assert.Equal(lastEditedBy, actual.LastEditedBy);
    }

    /// <summary>
    /// The details read returns each rule with the collections it governs. Both stacks assemble that from a second
    /// query keyed by rule -- MSSQL returns a second result set, EF groups in memory -- so this pins that the
    /// collections land on the right rule and that an ungoverning rule comes back with an empty list rather than
    /// null or another rule's collections.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task GetManyDetailsByOrganizationIdAsync_GroupsGovernedCollectionsByRule(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var governing = await CreateRuleAsync(accessRuleRepository, organization.Id, "Governing");
        var ungoverning = await CreateRuleAsync(accessRuleRepository, organization.Id, "Ungoverning");

        var firstCollection = await collectionRepository.CreateTestCollectionAsync(organization, "first");
        var secondCollection = await collectionRepository.CreateTestCollectionAsync(organization, "second");
        await collectionRepository.CreateTestCollectionAsync(organization, "ungoverned");

        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, governing.Id, [firstCollection.Id, secondCollection.Id], []);

        // Act
        var actual = await accessRuleRepository.GetManyDetailsByOrganizationIdAsync(organization.Id);

        // Assert
        Assert.Equal(2, actual.Count);

        var actualGoverning = Assert.Single(actual, r => r.Id == governing.Id);
        Assert.Equal(
            new[] { firstCollection.Id, secondCollection.Id }.Order(),
            actualGoverning.CollectionIds.Order());

        var actualUngoverning = Assert.Single(actual, r => r.Id == ungoverning.Id);
        Assert.Empty(actualUngoverning.CollectionIds);
    }

    /// <summary>
    /// The details read is org-scoped on both the rules and the collections hung off them, so neither another
    /// organization's rules nor its collection IDs may appear.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task GetManyDetailsByOrganizationIdAsync_ReturnsOnlyTheOrganizationsRules(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync(identifier: "owner");
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync(identifier: "other");

        var rule = await CreateRuleAsync(accessRuleRepository, organization.Id, "Own");
        var foreignRule = await CreateRuleAsync(accessRuleRepository, otherOrganization.Id, "Foreign");

        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var foreignCollection = await collectionRepository.CreateTestCollectionAsync(otherOrganization);

        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, rule.Id, [collection.Id], []);
        await collectionRepository.SetAccessRuleAssociationsAsync(
            otherOrganization.Id, foreignRule.Id, [foreignCollection.Id], []);

        // Act
        var actual = await accessRuleRepository.GetManyDetailsByOrganizationIdAsync(organization.Id);

        // Assert
        var actualRule = Assert.Single(actual);
        Assert.Equal(rule.Id, actualRule.Id);
        Assert.Equal([collection.Id], actualRule.CollectionIds);
    }

    private static Task<AccessRule> CreateRuleAsync(
        IAccessRuleRepository accessRuleRepository,
        Guid organizationId,
        string name)
        => accessRuleRepository.CreateAsync(new AccessRule
        {
            OrganizationId = organizationId,
            Name = name,
            Conditions = _conditions,
        });
}
