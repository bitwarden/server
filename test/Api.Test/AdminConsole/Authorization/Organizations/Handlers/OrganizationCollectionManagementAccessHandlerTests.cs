#nullable enable
using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization;

[SutProviderCustomize]
public class OrganizationCollectionManagementAccessHandlerTests
{
    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_WhenUserIdIsNull_NotAuthorized(
        Guid orgId, SutProvider<OrganizationCollectionManagementAccessHandler> sutProvider)
    {
        ArrangeRoute(sutProvider, orgId, null);

        var context = Authorize(sutProvider);

        Assert.False(context.HasSucceeded);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs()
            .GetManySharedByOrganizationIdWithPermissionsAsync(default, default, default);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task HandleRequirementAsync_WhenOwnerOrAdmin_Authorized_WithoutQueryingCollections(
        OrganizationUserType type, Guid orgId, Guid userId, User user,
        SutProvider<OrganizationCollectionManagementAccessHandler> sutProvider)
    {
        ArrangeRoute(sutProvider, orgId, userId);
        ArrangeOrganizationAbility(sutProvider, orgId, limitCollectionCreation: true);
        var claimsPrincipal = BuildClaimsPrincipal(user, new CurrentContextOrganization { Id = orgId, Type = type });

        var context = Authorize(sutProvider, claimsPrincipal);

        Assert.True(context.HasSucceeded);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs()
            .GetManySharedByOrganizationIdWithPermissionsAsync(default, default, default);
    }

    [Theory]
    [BitAutoData(true, false, false)]
    [BitAutoData(false, true, false)]
    [BitAutoData(false, false, true)]
    public async Task HandleRequirementAsync_WhenCustomUserManagesUsersOrGroupsOrAccessesReports_Authorized_WithoutQueryingCollections(
        bool manageUsers, bool manageGroups, bool accessReports, Guid orgId, Guid userId, User user,
        SutProvider<OrganizationCollectionManagementAccessHandler> sutProvider)
    {
        ArrangeRoute(sutProvider, orgId, userId);
        ArrangeOrganizationAbility(sutProvider, orgId, limitCollectionCreation: true);
        var claimsPrincipal = BuildClaimsPrincipal(user, new CurrentContextOrganization
        {
            Id = orgId,
            Type = OrganizationUserType.Custom,
            Permissions = new Permissions
            {
                ManageUsers = manageUsers,
                ManageGroups = manageGroups,
                AccessReports = accessReports,
            }
        });

        var context = Authorize(sutProvider, claimsPrincipal);

        Assert.True(context.HasSucceeded);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs()
            .GetManySharedByOrganizationIdWithPermissionsAsync(default, default, default);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_WhenCustomUserHasUnrelatedPermissionAndDoesNotManageAnyCollection_NotAuthorized(
        Guid orgId, Guid userId, User user, List<CollectionAdminDetails> collections,
        SutProvider<OrganizationCollectionManagementAccessHandler> sutProvider)
    {
        ArrangeRoute(sutProvider, orgId, userId);
        ArrangeOrganizationAbility(sutProvider, orgId, limitCollectionCreation: true);
        var claimsPrincipal = BuildClaimsPrincipal(user, new CurrentContextOrganization
        {
            Id = orgId,
            Type = OrganizationUserType.Custom,
            Permissions = new Permissions { AccessEventLogs = true, ManagePolicies = true },
        });

        collections.ForEach(c => c.Manage = false);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManySharedByOrganizationIdWithPermissionsAsync(orgId, userId, false)
            .Returns(collections);
        ArrangeProvider(sutProvider, userId, orgId, isProvider: false);

        var context = Authorize(sutProvider, claimsPrincipal);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_WhenMemberCannotCreateButManagesACollection_Authorized(
        Guid orgId, Guid userId, User user, List<CollectionAdminDetails> collections,
        SutProvider<OrganizationCollectionManagementAccessHandler> sutProvider)
    {
        ArrangeRoute(sutProvider, orgId, userId);
        ArrangeOrganizationAbility(sutProvider, orgId, limitCollectionCreation: true);
        var claimsPrincipal = BuildClaimsPrincipal(user,
            new CurrentContextOrganization { Id = orgId, Type = OrganizationUserType.User });

        collections[0].Manage = true;
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManySharedByOrganizationIdWithPermissionsAsync(orgId, userId, false)
            .Returns(collections);

        var context = Authorize(sutProvider, claimsPrincipal);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_WhenMemberCannotCreateAndDoesNotManageAnyCollection_NotAuthorized(
        Guid orgId, Guid userId, User user, List<CollectionAdminDetails> collections,
        SutProvider<OrganizationCollectionManagementAccessHandler> sutProvider)
    {
        ArrangeRoute(sutProvider, orgId, userId);
        ArrangeOrganizationAbility(sutProvider, orgId, limitCollectionCreation: true);
        var claimsPrincipal = BuildClaimsPrincipal(user,
            new CurrentContextOrganization { Id = orgId, Type = OrganizationUserType.User });

        collections.ForEach(c => c.Manage = false);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManySharedByOrganizationIdWithPermissionsAsync(orgId, userId, false)
            .Returns(collections);
        ArrangeProvider(sutProvider, userId, orgId, isProvider: false);

        var context = Authorize(sutProvider, claimsPrincipal);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_WhenNotMemberButProvider_Authorized_WithoutQueryingCollections(
        Guid orgId, Guid userId, SutProvider<OrganizationCollectionManagementAccessHandler> sutProvider)
    {
        ArrangeRoute(sutProvider, orgId, userId);
        ArrangeOrganizationAbility(sutProvider, orgId, limitCollectionCreation: true);
        ArrangeProvider(sutProvider, userId, orgId, isProvider: true);

        var context = Authorize(sutProvider, new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.True(context.HasSucceeded);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs()
            .GetManySharedByOrganizationIdWithPermissionsAsync(default, default, default);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_WhenNotMemberOrProvider_NotAuthorized(
        Guid orgId, Guid userId, SutProvider<OrganizationCollectionManagementAccessHandler> sutProvider)
    {
        ArrangeRoute(sutProvider, orgId, userId);
        ArrangeOrganizationAbility(sutProvider, orgId, limitCollectionCreation: true);
        ArrangeProvider(sutProvider, userId, orgId, isProvider: false);

        var context = Authorize(sutProvider, new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.False(context.HasSucceeded);
    }

    private static AuthorizationHandlerContext Authorize(
        SutProvider<OrganizationCollectionManagementAccessHandler> sutProvider, ClaimsPrincipal? claimsPrincipal = null)
    {
        var httpContext = sutProvider.GetDependency<IHttpContextAccessor>().HttpContext!;
        httpContext.User = claimsPrincipal ?? new ClaimsPrincipal(new ClaimsIdentity());

        var requirement = new OrganizationCollectionManagementAccessRequirement();
        var context = new AuthorizationHandlerContext([requirement], httpContext.User, null);

        sutProvider.Sut.HandleAsync(context).GetAwaiter().GetResult();
        return context;
    }

    private static void ArrangeRoute(
        SutProvider<OrganizationCollectionManagementAccessHandler> sutProvider, Guid orgId, Guid? userId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["orgId"] = orgId.ToString();
        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext = httpContext;
        sutProvider.GetDependency<IUserService>().GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
    }

    private static void ArrangeOrganizationAbility(
        SutProvider<OrganizationCollectionManagementAccessHandler> sutProvider, Guid orgId, bool limitCollectionCreation)
    {
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(orgId)
            .Returns(new OrganizationAbility { Id = orgId, LimitCollectionCreation = limitCollectionCreation });
    }

    private static void ArrangeProvider(
        SutProvider<OrganizationCollectionManagementAccessHandler> sutProvider, Guid userId, Guid orgId, bool isProvider)
    {
        var organizations = isProvider
            ? [new ProviderUserOrganizationDetails { OrganizationId = orgId }]
            : Array.Empty<ProviderUserOrganizationDetails>();

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyOrganizationDetailsByUserAsync(userId, ProviderUserStatusType.Confirmed)
            .Returns(organizations);
    }

    private static ClaimsPrincipal BuildClaimsPrincipal(User user, CurrentContextOrganization organization)
    {
        var claims = CoreHelpers.BuildIdentityClaims(user, [organization], [], false)
            .Select(c => new Claim(c.Key, c.Value));

        var claimsPrincipal = new ClaimsPrincipal();
        claimsPrincipal.AddIdentities([new ClaimsIdentity(claims)]);
        return claimsPrincipal;
    }
}
