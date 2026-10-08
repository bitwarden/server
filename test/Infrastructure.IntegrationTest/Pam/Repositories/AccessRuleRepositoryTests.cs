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

        var details = await accessRuleRepository.GetDetailsByIdAsync(rule.Id);
        Assert.NotNull(details);
        Assert.Contains(collection.Id, details.CollectionIds);

        // Act
        await accessRuleRepository.DeleteAsync(rule);

        // Assert
        Assert.Null(await accessRuleRepository.GetByIdAsync(rule.Id));

        var actualCollection = await collectionRepository.GetByIdAsync(collection.Id);
        Assert.NotNull(actualCollection);
        Assert.Null(actualCollection.AccessRuleId);
    }

    /// <summary>
    /// FK_AccessRequest_AccessRule does not cascade (NO ACTION on SQL Server, RESTRICT on EF), so the delete has to
    /// detach pinned requests first.
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
            CreationDate = now,
            RuleId = rule.Id,
        });
        Assert.Equal(rule.Id, (await accessRequestRepository.GetByIdAsync(request.Id))!.RuleId);

        // Act
        await accessRuleRepository.DeleteAsync(rule);

        // Assert: detached rather than deleted, since the request records what was granted.
        Assert.Null(await accessRuleRepository.GetByIdAsync(rule.Id));

        var persisted = await accessRequestRepository.GetByIdAsync(request.Id);
        Assert.NotNull(persisted);
        Assert.Null(persisted!.RuleId);
        Assert.Equal(AccessRequestAction.None, persisted.Action);
    }

    /// <summary>
    /// Organization cascades to AccessRequest and AccessLease, which reference each other under RESTRICT, so the
    /// delete must not depend on which cascade a provider fires first.
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

        var request = await accessRequestRepository.CreateAsync(new AccessRequest
        {
            OrganizationId = organization.Id,
            CollectionId = collection.Id,
            CipherId = cipherId,
            RequesterId = requesterId,
            NotBefore = now.AddMinutes(-5),
            NotAfter = now.AddHours(1),
            Action = AccessRequestAction.Approved,
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
            Action = AccessLeaseAction.None,
            NotBefore = request.NotBefore,
            NotAfter = request.NotAfter,
            CreationDate = now,
        };
        Assert.Equal(AccessLeaseMintOutcome.Minted,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(lease, now, false));

        // The extension points back at the lease, closing the reference cycle.
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
            Action = AccessRequestAction.Approved,
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
            await accessRequestRepository.CreateApprovedExtensionAsync(
                extension, extensionDecision, now, denialComment: null));

        // Act
        await organizationRepository.DeleteAsync(organization);

        // Assert
        Assert.Null(await organizationRepository.GetByIdAsync(organization.Id));
        Assert.Null(await accessRuleRepository.GetByIdAsync(rule.Id));
        Assert.Null(await accessLeaseRepository.GetByIdAsync(lease.Id));
        Assert.Null(await accessRequestRepository.GetByIdAsync(request.Id));
        Assert.Null(await accessRequestRepository.GetByIdAsync(extension.Id));
    }

    /// <summary>
    /// Organization cascades to Collection and AccessRule while Collection does not cascade to AccessRule, so the
    /// delete must not depend on which cascade path a provider applies first.
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

        // Assert
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

        // Act: a hard delete frees the name in the unique (OrganizationId, Name) index.
        await accessRuleRepository.DeleteAsync(original);

        var recreated = await accessRuleRepository.CreateAsync(new AccessRule
        {
            OrganizationId = organization.Id,
            Name = "Reusable Name",
            Conditions = """{"kind":"human_approval"}""",
        });

        // Assert
        Assert.NotEqual(original.Id, recreated.Id);
        Assert.Null(await accessRuleRepository.GetByIdAsync(original.Id));

        var live = await accessRuleRepository.GetByIdAsync(recreated.Id);
        Assert.NotNull(live);
        Assert.Equal("Reusable Name", live.Name);
    }

    /// <summary>Leaking another organization's rule would expose the conditions gating its data.</summary>
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
    /// Dapper maps the sproc's columns straight onto the entity while EF maps through AutoMapper, so a column missing
    /// on one side diverges silently.
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
    /// Both stacks attach collections to each <see cref="AccessRuleDetails"/> from a second query, so an ungoverning
    /// rule has to get an empty list rather than null or another rule's collections.
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

    /// <summary>Scoped on the collections as well as the rules.</summary>
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
