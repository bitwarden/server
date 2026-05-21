using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Collections;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.PrivilegedAccessManagement.Services;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Collections;

[SutProviderCustomize]
[OrganizationCustomize]
public class CreateCollectionCommandTests
{
    [Theory, BitAutoData]
    public async Task CreateAsync_WithoutGroupsAndUsers_CreatesCollection(
        Organization organization, Collection collection,
        SutProvider<CreateCollectionCommand> sutProvider)
    {
        collection.Id = default;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
        var utcNow = DateTime.UtcNow;

        await sutProvider.Sut.CreateAsync(collection, null, null);

        await sutProvider.GetDependency<ICollectionRepository>()
            .Received(1)
            .CreateAsync(
                collection,
                Arg.Is<List<CollectionAccessSelection>>(l => l == null),
                Arg.Is<List<CollectionAccessSelection>>(l => l == null));
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogCollectionEventAsync(collection, EventType.Collection_Created);
        Assert.True(collection.CreationDate - utcNow < TimeSpan.FromSeconds(1));
        Assert.True(collection.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_WithGroupsAndUsers_CreatesCollectionWithGroupsAndUsers(
        Organization organization, Collection collection,
        [CollectionAccessSelectionCustomize(true)] IEnumerable<CollectionAccessSelection> groups,
        IEnumerable<CollectionAccessSelection> users,
        SutProvider<CreateCollectionCommand> sutProvider)
    {
        collection.Id = default;
        organization.UseGroups = true;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
        var utcNow = DateTime.UtcNow;

        await sutProvider.Sut.CreateAsync(collection, groups, users);

        await sutProvider.GetDependency<ICollectionRepository>()
            .Received(1)
            .CreateAsync(
                collection,
                Arg.Is<List<CollectionAccessSelection>>(l => l.Any(i => i.Manage == true)),
                Arg.Any<List<CollectionAccessSelection>>());
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogCollectionEventAsync(collection, EventType.Collection_Created);
        Assert.True(collection.CreationDate - utcNow < TimeSpan.FromSeconds(1));
        Assert.True(collection.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_WithOrganizationUseGroupDisabled_CreatesCollectionWithoutGroups(
        Organization organization, Collection collection,
        [CollectionAccessSelectionCustomize] IEnumerable<CollectionAccessSelection> groups,
        [CollectionAccessSelectionCustomize(true)] IEnumerable<CollectionAccessSelection> users,
        SutProvider<CreateCollectionCommand> sutProvider)
    {
        collection.Id = default;
        organization.UseGroups = false;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
        var utcNow = DateTime.UtcNow;

        await sutProvider.Sut.CreateAsync(collection, groups, users);

        await sutProvider.GetDependency<ICollectionRepository>()
            .Received(1)
            .CreateAsync(
                collection,
                Arg.Is<List<CollectionAccessSelection>>(l => l == null),
                Arg.Is<List<CollectionAccessSelection>>(l => l.Any(i => i.Manage == true)));
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogCollectionEventAsync(collection, EventType.Collection_Created);
        Assert.True(collection.CreationDate - utcNow < TimeSpan.FromSeconds(1));
        Assert.True(collection.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_WithNonExistingOrganizationId_ThrowsBadRequest(
        Collection collection, SutProvider<CreateCollectionCommand> sutProvider)
    {
        collection.Id = default;
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(collection));
        Assert.Contains("Organization not found", ex.Message);
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default, default, default);
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogCollectionEventAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_WithoutManageAccess_ThrowsBadRequest(
        Organization organization, Collection collection,
        [CollectionAccessSelectionCustomize] IEnumerable<CollectionAccessSelection> users,
        SutProvider<CreateCollectionCommand> sutProvider)
    {
        collection.Id = default;
        organization.AllowAdminAccessToAllCollectionItems = false;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(collection, null, users));
        Assert.Contains("At least one member or group must have can manage permission.", ex.Message);
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default, default, default);
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogCollectionEventAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_WithExceedsOrganizationMaxCollections_ThrowsBadRequest(
        Organization organization, Collection collection,
        [CollectionAccessSelectionCustomize(true)] IEnumerable<CollectionAccessSelection> users,
        SutProvider<CreateCollectionCommand> sutProvider)
    {
        collection.Id = default;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetCountByOrganizationIdAsync(organization.Id)
            .Returns(organization.MaxCollections.Value);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(collection, null, users));
        Assert.Equal($@"You have reached the maximum number of collections ({organization.MaxCollections.Value}) for this organization.", ex.Message);
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default, default, default);
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogCollectionEventAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_WithInvalidManageAssociations_ThrowsBadRequest(
        Organization organization, Collection collection, SutProvider<CreateCollectionCommand> sutProvider)
    {
        collection.Id = default;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var invalidGroups = new List<CollectionAccessSelection>
        {
            new() { Id = Guid.NewGuid(), Manage = true, ReadOnly = true }
        };

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(collection, invalidGroups, null));
        Assert.Contains("The Manage property is mutually exclusive and cannot be true while the ReadOnly or HidePasswords properties are also true.", ex.Message);
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default, default, default);
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogCollectionEventAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_WithDefaultUserCollectionType_ThrowsBadRequest(
        Organization organization, Collection collection, SutProvider<CreateCollectionCommand> sutProvider)
    {
        collection.Id = default;
        collection.Type = CollectionType.DefaultUserCollection;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(collection));
        Assert.Contains("You cannot create a collection with the type as DefaultUserCollection.", ex.Message);
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default, default, default);
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogCollectionEventAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_PamFlagOff_ClearsLeasingFields(
        Organization organization, Collection collection,
        [CollectionAccessSelectionCustomize(true)] IEnumerable<CollectionAccessSelection> users,
        SutProvider<CreateCollectionCommand> sutProvider)
    {
        collection.Id = default;
        collection.LeasingEnabled = true;
        collection.LeasingPolicy = """{"kind":"human_approval"}""";
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.Pam)
            .Returns(false);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        await sutProvider.Sut.CreateAsync(collection, null, users);

        Assert.False(collection.LeasingEnabled);
        Assert.Null(collection.LeasingPolicy);
        sutProvider.GetDependency<ILeasingPolicyValidator>()
            .DidNotReceiveWithAnyArgs()
            .Validate(default);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_PamFlagOn_InvalidPolicy_ThrowsBadRequest(
        Organization organization, Collection collection,
        [CollectionAccessSelectionCustomize(true)] IEnumerable<CollectionAccessSelection> users,
        SutProvider<CreateCollectionCommand> sutProvider)
    {
        collection.Id = default;
        collection.LeasingEnabled = true;
        collection.LeasingPolicy = """{"kind":"bogus"}""";
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.Pam)
            .Returns(true);
        sutProvider.GetDependency<ILeasingPolicyValidator>()
            .Validate(collection.LeasingPolicy)
            .Returns(LeasingPolicyValidationResult.Invalid("Unsupported policy kind"));
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(collection, null, users));
        Assert.Equal("Unsupported policy kind", ex.Message);
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default, default, default);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_PamFlagOn_ValidPolicy_RetainsLeasingFields(
        Organization organization, Collection collection,
        [CollectionAccessSelectionCustomize(true)] IEnumerable<CollectionAccessSelection> users,
        SutProvider<CreateCollectionCommand> sutProvider)
    {
        collection.Id = default;
        collection.LeasingEnabled = true;
        collection.LeasingPolicy = """{"kind":"human_approval"}""";
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.Pam)
            .Returns(true);
        sutProvider.GetDependency<ILeasingPolicyValidator>()
            .Validate(collection.LeasingPolicy)
            .Returns(LeasingPolicyValidationResult.Valid);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        await sutProvider.Sut.CreateAsync(collection, null, users);

        Assert.True(collection.LeasingEnabled);
        Assert.Equal("""{"kind":"human_approval"}""", collection.LeasingPolicy);
    }
}
