using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Services;

[SutProviderCustomize]
public class ApproverCollectionAccessQueryTests
{
    [Theory, BitAutoData]
    public async Task GetManageableCollectionIdsAsync_ReturnsOnlyAssignedManageCollections(
        SutProvider<ApproverCollectionAccessQuery> sutProvider, Guid userId, Guid manageId, Guid readOnlyId)
    {
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(userId).Returns(new List<CollectionDetails>
        {
            new() { Id = manageId, Manage = true },
            new() { Id = readOnlyId, Manage = false },
        });
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns(new List<CurrentContextOrganization>());

        var result = await sutProvider.Sut.GetManageableCollectionIdsAsync(userId);

        Assert.Contains(manageId, result);
        Assert.DoesNotContain(readOnlyId, result);
    }

    [Theory, BitAutoData]
    public async Task GetManageableCollectionIdsAsync_OwnerWithAdminAccess_IncludesAllOrgCollections(
        SutProvider<ApproverCollectionAccessQuery> sutProvider, Guid userId, Guid orgId, Guid orgCollectionId)
    {
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(userId)
            .Returns(new List<CollectionDetails>());
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns(new List<CurrentContextOrganization>
        {
            new() { Id = orgId, Type = OrganizationUserType.Owner },
        });
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(orgId)
            .Returns(new OrganizationAbility { Id = orgId, Enabled = true, AllowAdminAccessToAllCollectionItems = true });
        sutProvider.GetDependency<ICollectionRepository>().GetManyByOrganizationIdAsync(orgId)
            .Returns(new List<Collection> { new() { Id = orgCollectionId, OrganizationId = orgId } });

        var result = await sutProvider.Sut.GetManageableCollectionIdsAsync(userId);

        Assert.Contains(orgCollectionId, result);
    }

    [Theory, BitAutoData]
    public async Task GetManageableCollectionIdsAsync_OwnerWithoutAdminAccess_DoesNotIncludeAllOrgCollections(
        SutProvider<ApproverCollectionAccessQuery> sutProvider, Guid userId, Guid orgId)
    {
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(userId)
            .Returns(new List<CollectionDetails>());
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns(new List<CurrentContextOrganization>
        {
            new() { Id = orgId, Type = OrganizationUserType.Owner },
        });
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(orgId)
            .Returns(new OrganizationAbility { Id = orgId, Enabled = true, AllowAdminAccessToAllCollectionItems = false });

        var result = await sutProvider.Sut.GetManageableCollectionIdsAsync(userId);

        Assert.Empty(result);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task GetManageableCollectionIdsAsync_EditAnyCollection_IncludesAllOrgCollections(
        SutProvider<ApproverCollectionAccessQuery> sutProvider, Guid userId, Guid orgId, Guid orgCollectionId)
    {
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(userId)
            .Returns(new List<CollectionDetails>());
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns(new List<CurrentContextOrganization>
        {
            new() { Id = orgId, Type = OrganizationUserType.Custom, Permissions = new Permissions { EditAnyCollection = true } },
        });
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(orgId)
            .Returns(new OrganizationAbility { Id = orgId, Enabled = true });
        sutProvider.GetDependency<ICollectionRepository>().GetManyByOrganizationIdAsync(orgId)
            .Returns(new List<Collection> { new() { Id = orgCollectionId, OrganizationId = orgId } });

        var result = await sutProvider.Sut.GetManageableCollectionIdsAsync(userId);

        Assert.Contains(orgCollectionId, result);
    }

    [Theory, BitAutoData]
    public async Task GetManageableCollectionIdsAsync_OwnerOfSuspendedOrg_DoesNotIncludeOrgCollections(
        SutProvider<ApproverCollectionAccessQuery> sutProvider, Guid userId, Guid orgId)
    {
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(userId)
            .Returns(new List<CollectionDetails>());
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns(new List<CurrentContextOrganization>
        {
            new() { Id = orgId, Type = OrganizationUserType.Owner },
        });
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(orgId)
            .Returns(new OrganizationAbility { Id = orgId, Enabled = false, AllowAdminAccessToAllCollectionItems = true });

        var result = await sutProvider.Sut.GetManageableCollectionIdsAsync(userId);

        Assert.Empty(result);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task CanManageCollectionAsync_AssignedWithManage_ReturnsTrueWithoutLookingUpTheCollection(
        SutProvider<ApproverCollectionAccessQuery> sutProvider, Guid userId, Guid collectionId)
    {
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(userId).Returns(new List<CollectionDetails>
        {
            new() { Id = collectionId, Manage = true },
        });

        Assert.True(await sutProvider.Sut.CanManageCollectionAsync(userId, collectionId));
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task CanManageCollectionAsync_AssignedReadOnly_ReturnsFalse(
        SutProvider<ApproverCollectionAccessQuery> sutProvider, Guid userId, Guid orgId, Guid collectionId)
    {
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(userId).Returns(new List<CollectionDetails>
        {
            new() { Id = collectionId, Manage = false },
        });
        sutProvider.GetDependency<ICollectionRepository>().GetByIdAsync(collectionId)
            .Returns(new Collection { Id = collectionId, OrganizationId = orgId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(orgId)
            .Returns(new CurrentContextOrganization { Id = orgId, Type = OrganizationUserType.User });

        Assert.False(await sutProvider.Sut.CanManageCollectionAsync(userId, collectionId));
    }

    [Theory, BitAutoData]
    public async Task CanManageCollectionAsync_OwnerWithAdminAccess_ReturnsTrueWithoutFetchingOrgCollections(
        SutProvider<ApproverCollectionAccessQuery> sutProvider, Guid userId, Guid orgId, Guid collectionId)
    {
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(userId)
            .Returns(new List<CollectionDetails>());
        sutProvider.GetDependency<ICollectionRepository>().GetByIdAsync(collectionId)
            .Returns(new Collection { Id = collectionId, OrganizationId = orgId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(orgId)
            .Returns(new CurrentContextOrganization { Id = orgId, Type = OrganizationUserType.Owner });
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(orgId)
            .Returns(new OrganizationAbility { Id = orgId, Enabled = true, AllowAdminAccessToAllCollectionItems = true });

        Assert.True(await sutProvider.Sut.CanManageCollectionAsync(userId, collectionId));
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task CanManageCollectionAsync_OwnerWithoutAdminAccess_ReturnsFalse(
        SutProvider<ApproverCollectionAccessQuery> sutProvider, Guid userId, Guid orgId, Guid collectionId)
    {
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(userId)
            .Returns(new List<CollectionDetails>());
        sutProvider.GetDependency<ICollectionRepository>().GetByIdAsync(collectionId)
            .Returns(new Collection { Id = collectionId, OrganizationId = orgId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(orgId)
            .Returns(new CurrentContextOrganization { Id = orgId, Type = OrganizationUserType.Owner });
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(orgId)
            .Returns(new OrganizationAbility { Id = orgId, Enabled = true, AllowAdminAccessToAllCollectionItems = false });

        Assert.False(await sutProvider.Sut.CanManageCollectionAsync(userId, collectionId));
    }

    [Theory, BitAutoData]
    public async Task CanManageCollectionAsync_EditAnyCollection_ReturnsTrue(
        SutProvider<ApproverCollectionAccessQuery> sutProvider, Guid userId, Guid orgId, Guid collectionId)
    {
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(userId)
            .Returns(new List<CollectionDetails>());
        sutProvider.GetDependency<ICollectionRepository>().GetByIdAsync(collectionId)
            .Returns(new Collection { Id = collectionId, OrganizationId = orgId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(orgId).Returns(new CurrentContextOrganization
        {
            Id = orgId,
            Type = OrganizationUserType.Custom,
            Permissions = new Permissions { EditAnyCollection = true },
        });
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(orgId)
            .Returns(new OrganizationAbility { Id = orgId, Enabled = true });

        Assert.True(await sutProvider.Sut.CanManageCollectionAsync(userId, collectionId));
    }

    [Theory, BitAutoData]
    public async Task CanManageCollectionAsync_EditAnyCollectionInSuspendedOrg_ReturnsFalse(
        SutProvider<ApproverCollectionAccessQuery> sutProvider, Guid userId, Guid orgId, Guid collectionId)
    {
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(userId)
            .Returns(new List<CollectionDetails>());
        sutProvider.GetDependency<ICollectionRepository>().GetByIdAsync(collectionId)
            .Returns(new Collection { Id = collectionId, OrganizationId = orgId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(orgId).Returns(new CurrentContextOrganization
        {
            Id = orgId,
            Type = OrganizationUserType.Custom,
            Permissions = new Permissions { EditAnyCollection = true },
        });
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(orgId)
            .Returns(new OrganizationAbility { Id = orgId, Enabled = false });

        Assert.False(await sutProvider.Sut.CanManageCollectionAsync(userId, collectionId));
    }

    [Theory, BitAutoData]
    public async Task CanManageCollectionAsync_NotAMemberOfTheCollectionsOrg_ReturnsFalse(
        SutProvider<ApproverCollectionAccessQuery> sutProvider, Guid userId, Guid orgId, Guid collectionId)
    {
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(userId)
            .Returns(new List<CollectionDetails>());
        sutProvider.GetDependency<ICollectionRepository>().GetByIdAsync(collectionId)
            .Returns(new Collection { Id = collectionId, OrganizationId = orgId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(orgId).Returns((CurrentContextOrganization?)null);

        Assert.False(await sutProvider.Sut.CanManageCollectionAsync(userId, collectionId));
    }

    [Theory, BitAutoData]
    public async Task CanManageCollectionAsync_MissingCollection_ReturnsFalse(
        SutProvider<ApproverCollectionAccessQuery> sutProvider, Guid userId, Guid collectionId)
    {
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(userId)
            .Returns(new List<CollectionDetails>());
        sutProvider.GetDependency<ICollectionRepository>().GetByIdAsync(collectionId).Returns((Collection?)null);

        Assert.False(await sutProvider.Sut.CanManageCollectionAsync(userId, collectionId));
    }
}
