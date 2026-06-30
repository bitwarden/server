using Bit.Commercial.Pam.Services;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Pam.Test.Services;

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
            .Returns(new OrganizationAbility { Id = orgId, AllowAdminAccessToAllCollectionItems = true });
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
            .Returns(new OrganizationAbility { Id = orgId, AllowAdminAccessToAllCollectionItems = false });

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
        sutProvider.GetDependency<ICollectionRepository>().GetManyByOrganizationIdAsync(orgId)
            .Returns(new List<Collection> { new() { Id = orgCollectionId, OrganizationId = orgId } });

        var result = await sutProvider.Sut.GetManageableCollectionIdsAsync(userId);

        Assert.Contains(orgCollectionId, result);
    }

    [Theory, BitAutoData]
    public async Task CanManageCollectionAsync_DelegatesToManageableSet(
        SutProvider<ApproverCollectionAccessQuery> sutProvider, Guid userId, Guid manageId, Guid otherId)
    {
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(userId).Returns(new List<CollectionDetails>
        {
            new() { Id = manageId, Manage = true },
        });
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns(new List<CurrentContextOrganization>());

        Assert.True(await sutProvider.Sut.CanManageCollectionAsync(userId, manageId));
        Assert.False(await sutProvider.Sut.CanManageCollectionAsync(userId, otherId));
    }
}
