using Bit.Services.Pam.Services;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
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
        NoOtherMemberships(sutProvider, userId);

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
        NoOtherMemberships(sutProvider, userId);
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
        NoOtherMemberships(sutProvider, userId);
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
        NoOtherMemberships(sutProvider, userId);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByOrganizationIdAsync(orgId)
            .Returns(new List<Collection> { new() { Id = orgCollectionId, OrganizationId = orgId } });

        var result = await sutProvider.Sut.GetManageableCollectionIdsAsync(userId);

        Assert.Contains(orgCollectionId, result);
    }

    // A suspended (disabled) org is absent from the claim-based request context, but governance must still load: the
    // user's confirmed membership is read from the database (which includes disabled orgs) and folded in.
    [Theory, BitAutoData]
    public async Task GetManageableCollectionIdsAsync_SuspendedOrgDroppedFromContext_StillIncludesOrgCollections(
        SutProvider<ApproverCollectionAccessQuery> sutProvider, Guid userId, Guid orgId, Guid orgCollectionId)
    {
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(userId)
            .Returns(new List<CollectionDetails>());
        // The suspended org is gone from the request context.
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns(new List<CurrentContextOrganization>());
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByUserAsync(userId, OrganizationUserStatusType.Confirmed)
            .Returns(new List<OrganizationUserOrganizationDetails>
            {
                new()
                {
                    OrganizationId = orgId,
                    Type = OrganizationUserType.Owner,
                    Status = OrganizationUserStatusType.Confirmed,
                    Enabled = false,
                },
            });
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(orgId)
            .Returns(new OrganizationAbility { Id = orgId, AllowAdminAccessToAllCollectionItems = true });
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
        NoOtherMemberships(sutProvider, userId);

        Assert.True(await sutProvider.Sut.CanManageCollectionAsync(userId, manageId));
        Assert.False(await sutProvider.Sut.CanManageCollectionAsync(userId, otherId));
    }

    // The manage-all path also reads confirmed memberships (to catch suspended orgs the request context drops); most
    // tests have none beyond what the context already covers.
    private static void NoOtherMemberships(SutProvider<ApproverCollectionAccessQuery> sutProvider, Guid userId)
        => sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByUserAsync(userId, OrganizationUserStatusType.Confirmed)
            .Returns(new List<OrganizationUserOrganizationDetails>());
}
