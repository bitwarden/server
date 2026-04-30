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
public class GroupMembershipAuthorizationHandlerTests
{
    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_WithFeatureFlagDisabled_DoesNotSucceed(
        SutProvider<GroupMembershipAuthorizationHandler> sutProvider,
        GroupMembershipUpdateResource resource)
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
    public async Task HandleRequirementAsync_WithAllowAdminAccessEnabled_UserAddingSelf_Succeeds(
        SutProvider<GroupMembershipAuthorizationHandler> sutProvider,
        Guid actingUserId,
        Guid orgId)
    {
        ArrangeFeatureFlag(sutProvider);

        var actingOrgUserId = Guid.NewGuid();
        var resource = new GroupMembershipUpdateResource(
            orgId,
            actingUserId,
            PostedMemberOrganizationUserIds: [actingOrgUserId],
            CurrentMemberOrganizationUserIds: []);

        var orgUser = new OrganizationUser { Id = actingOrgUserId, UserId = actingUserId };
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(orgId, actingUserId)
            .Returns(orgUser);

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
    public async Task HandleRequirementAsync_WhenUserAlreadyInGroup_Succeeds(
        SutProvider<GroupMembershipAuthorizationHandler> sutProvider,
        Guid actingUserId,
        Guid orgId)
    {
        ArrangeFeatureFlag(sutProvider);

        var actingOrgUserId = Guid.NewGuid();
        var resource = new GroupMembershipUpdateResource(
            orgId,
            actingUserId,
            PostedMemberOrganizationUserIds: [actingOrgUserId],
            CurrentMemberOrganizationUserIds: [actingOrgUserId]);

        var orgUser = new OrganizationUser { Id = actingOrgUserId, UserId = actingUserId };
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(orgId, actingUserId)
            .Returns(orgUser);

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
    public async Task HandleRequirementAsync_WhenUserAddingSelf_ThrowsBadRequest(
        SutProvider<GroupMembershipAuthorizationHandler> sutProvider,
        Guid actingUserId,
        Guid orgId)
    {
        ArrangeFeatureFlag(sutProvider);

        var actingOrgUserId = Guid.NewGuid();
        var resource = new GroupMembershipUpdateResource(
            orgId,
            actingUserId,
            PostedMemberOrganizationUserIds: [actingOrgUserId],
            CurrentMemberOrganizationUserIds: []);

        var orgUser = new OrganizationUser { Id = actingOrgUserId, UserId = actingUserId };
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(orgId, actingUserId)
            .Returns(orgUser);

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
    public async Task HandleRequirementAsync_WhenProviderUser_OrgUserNull_Succeeds(
        SutProvider<GroupMembershipAuthorizationHandler> sutProvider,
        Guid actingUserId,
        Guid orgId)
    {
        ArrangeFeatureFlag(sutProvider);

        var resource = new GroupMembershipUpdateResource(
            orgId,
            actingUserId,
            PostedMemberOrganizationUserIds: [],
            CurrentMemberOrganizationUserIds: []);

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

    private static void ArrangeFeatureFlag(SutProvider<GroupMembershipAuthorizationHandler> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.CollectionUserCollectionGroupAuthorizationHandlers)
            .Returns(true);
    }
}
