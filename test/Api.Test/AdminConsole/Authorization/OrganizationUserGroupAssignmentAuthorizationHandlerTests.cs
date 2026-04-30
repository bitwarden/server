using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization;
using Bit.Core;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Authorization;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization;

[SutProviderCustomize]
public class OrganizationUserGroupAssignmentAuthorizationHandlerTests
{
    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_WithFeatureFlagDisabled_DoesNotSucceed(
        SutProvider<OrganizationUserGroupAssignmentAuthorizationHandler> sutProvider,
        OrganizationUserGroupAssignmentResource resource)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.CollectionUserCollectionGroupAuthorizationHandlers)
            .Returns(false);

        var context = new AuthorizationHandlerContext(
            new[] { GroupOperations.UpdateMembership },
            new ClaimsPrincipal(),
            resource);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_EditingOtherUser_Succeeds(
        SutProvider<OrganizationUserGroupAssignmentAuthorizationHandler> sutProvider,
        Guid actingUserId,
        Guid orgId)
    {
        ArrangeFeatureFlag(sutProvider);

        var actingOrgUserId = Guid.NewGuid();
        var targetOrgUserId = Guid.NewGuid();

        var resource = new OrganizationUserGroupAssignmentResource(
            orgId,
            ActingUserId: actingUserId,
            TargetOrganizationUserId: targetOrgUserId,
            PostedGroupIds: [Guid.NewGuid()],
            CurrentGroupIds: []);

        var actingOrgUser = new OrganizationUser { Id = actingOrgUserId, UserId = actingUserId };
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(orgId, actingUserId)
            .Returns(actingOrgUser);

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(orgId)
            .Returns(new OrganizationAbility { Id = orgId, AllowAdminAccessToAllCollectionItems = false });

        var context = new AuthorizationHandlerContext(
            new[] { GroupOperations.UpdateMembership },
            new ClaimsPrincipal(),
            resource);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_SelfEdit_AllowAdminAccessEnabled_Succeeds(
        SutProvider<OrganizationUserGroupAssignmentAuthorizationHandler> sutProvider,
        Guid actingUserId,
        Guid orgId)
    {
        ArrangeFeatureFlag(sutProvider);

        var actingOrgUserId = Guid.NewGuid();
        var resource = new OrganizationUserGroupAssignmentResource(
            orgId,
            ActingUserId: actingUserId,
            TargetOrganizationUserId: actingOrgUserId,
            PostedGroupIds: [Guid.NewGuid()],
            CurrentGroupIds: []);

        var actingOrgUser = new OrganizationUser { Id = actingOrgUserId, UserId = actingUserId };
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(orgId, actingUserId)
            .Returns(actingOrgUser);

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(orgId)
            .Returns(new OrganizationAbility { Id = orgId, AllowAdminAccessToAllCollectionItems = true });

        var context = new AuthorizationHandlerContext(
            new[] { GroupOperations.UpdateMembership },
            new ClaimsPrincipal(),
            resource);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_SelfEdit_AllowAdminAccessDisabled_AddingNewGroup_ThrowsBadRequest(
        SutProvider<OrganizationUserGroupAssignmentAuthorizationHandler> sutProvider,
        Guid actingUserId,
        Guid orgId)
    {
        ArrangeFeatureFlag(sutProvider);

        var actingOrgUserId = Guid.NewGuid();
        var newGroupId = Guid.NewGuid();
        var resource = new OrganizationUserGroupAssignmentResource(
            orgId,
            ActingUserId: actingUserId,
            TargetOrganizationUserId: actingOrgUserId,
            PostedGroupIds: [newGroupId],
            CurrentGroupIds: []);

        var actingOrgUser = new OrganizationUser { Id = actingOrgUserId, UserId = actingUserId };
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(orgId, actingUserId)
            .Returns(actingOrgUser);

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(orgId)
            .Returns(new OrganizationAbility { Id = orgId, AllowAdminAccessToAllCollectionItems = false });

        var context = new AuthorizationHandlerContext(
            new[] { GroupOperations.UpdateMembership },
            new ClaimsPrincipal(),
            resource);

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.HandleAsync(context));
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_SelfEdit_AllowAdminAccessDisabled_AlreadyInAllGroups_Succeeds(
        SutProvider<OrganizationUserGroupAssignmentAuthorizationHandler> sutProvider,
        Guid actingUserId,
        Guid orgId)
    {
        ArrangeFeatureFlag(sutProvider);

        var actingOrgUserId = Guid.NewGuid();
        var existingGroupId = Guid.NewGuid();
        var resource = new OrganizationUserGroupAssignmentResource(
            orgId,
            ActingUserId: actingUserId,
            TargetOrganizationUserId: actingOrgUserId,
            PostedGroupIds: [existingGroupId],
            CurrentGroupIds: [existingGroupId]);

        var actingOrgUser = new OrganizationUser { Id = actingOrgUserId, UserId = actingUserId };
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(orgId, actingUserId)
            .Returns(actingOrgUser);

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(orgId)
            .Returns(new OrganizationAbility { Id = orgId, AllowAdminAccessToAllCollectionItems = false });

        var context = new AuthorizationHandlerContext(
            new[] { GroupOperations.UpdateMembership },
            new ClaimsPrincipal(),
            resource);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_ProviderUser_OrgUserNull_Succeeds(
        SutProvider<OrganizationUserGroupAssignmentAuthorizationHandler> sutProvider,
        Guid actingUserId,
        Guid orgId,
        Guid targetOrgUserId)
    {
        ArrangeFeatureFlag(sutProvider);

        var resource = new OrganizationUserGroupAssignmentResource(
            orgId,
            ActingUserId: actingUserId,
            TargetOrganizationUserId: targetOrgUserId,
            PostedGroupIds: [Guid.NewGuid()],
            CurrentGroupIds: []);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(orgId, actingUserId)
            .Returns((OrganizationUser)null);

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(orgId)
            .Returns(new OrganizationAbility { Id = orgId, AllowAdminAccessToAllCollectionItems = false });

        var context = new AuthorizationHandlerContext(
            new[] { GroupOperations.UpdateMembership },
            new ClaimsPrincipal(),
            resource);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    private static void ArrangeFeatureFlag(
        SutProvider<OrganizationUserGroupAssignmentAuthorizationHandler> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.CollectionUserCollectionGroupAuthorizationHandlers)
            .Returns(true);
    }
}
