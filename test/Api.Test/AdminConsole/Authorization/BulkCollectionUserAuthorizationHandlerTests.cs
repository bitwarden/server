using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization.Collections;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.Vault.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization;

[SutProviderCustomize]
public class BulkCollectionUserAuthorizationHandlerTests
{
    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_WithFeatureFlagDisabled_DoesNotSucceed(
        SutProvider<BulkCollectionUserAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization,
        Guid actingUserId)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.CollectionUserCollectionGroupAuthorizationHandlers)
            .Returns(false);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperations.Create },
            new ClaimsPrincipal(),
            collections.ToList());

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_WithNoUserId_DoesNotSucceed(
        SutProvider<BulkCollectionUserAuthorizationHandler> sutProvider,
        ICollection<Collection> collections)
    {
        ArrangeFeatureFlag(sutProvider);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperations.Create },
            new ClaimsPrincipal(),
            collections.ToList());

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanCreate_WithAdminOrOwnerAndAllowAdminAccess_Succeeds(
        OrganizationUserType userType,
        Guid actingUserId,
        SutProvider<BulkCollectionUserAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        organization.Type = userType;
        organization.Permissions = new Permissions();

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, true);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperations.Create },
            new ClaimsPrincipal(),
            collections.ToList());

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreate_WithCustomManageUsersAndAllowAdminAccess_Succeeds(
        SutProvider<BulkCollectionUserAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization,
        Guid actingUserId)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { ManageUsers = true };

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, true);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperations.Create },
            new ClaimsPrincipal(),
            collections.ToList());

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreate_WithUserManagePermissionOnAllCollections_Succeeds(
        SutProvider<BulkCollectionUserAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization,
        Guid actingUserId)
    {
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        foreach (var c in collections)
        {
            c.Manage = true;
        }

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, false);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperations.Create },
            new ClaimsPrincipal(),
            collections.Cast<Collection>().ToList());

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreate_WithUserWithoutManagePermission_DoesNotSucceed(
        SutProvider<BulkCollectionUserAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization,
        Guid actingUserId)
    {
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, false);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperations.Create },
            new ClaimsPrincipal(),
            collections.ToList());

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByUserIdAsync(actingUserId)
            .Returns(new List<CollectionDetails>());
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>())
            .Returns(false);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreate_WithProviderUser_Succeeds(
        SutProvider<BulkCollectionUserAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        Guid actingUserId)
    {
        ArrangeFeatureFlag(sutProvider);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperations.Create },
            new ClaimsPrincipal(),
            collections.ToList());

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>())
            .Returns((CurrentContextOrganization)null);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>())
            .Returns(true);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreate_NoSelfAssignmentCheckPerformed(
        SutProvider<BulkCollectionUserAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization,
        Guid actingUserId)
    {
        organization.Type = OrganizationUserType.Owner;
        organization.Permissions = new Permissions();

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, false);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperations.Create },
            new ClaimsPrincipal(),
            collections.ToList());

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByUserIdAsync(actingUserId)
            .Returns(collections.Select(c => { var cd = new CollectionDetails(); cd.Id = c.Id; cd.Manage = true; return cd; }).ToList());

        // No organization user repository - self-assignment check does not apply to bulk handler.
        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    private static void ArrangeFeatureFlag(SutProvider<BulkCollectionUserAuthorizationHandler> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.CollectionUserCollectionGroupAuthorizationHandlers)
            .Returns(true);
    }

    private static void ArrangeOrganizationAbility(
        SutProvider<BulkCollectionUserAuthorizationHandler> sutProvider,
        CurrentContextOrganization organization,
        bool allowAdminAccessToAllCollectionItems)
    {
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility
            {
                Id = organization.Id,
                AllowAdminAccessToAllCollectionItems = allowAdminAccessToAllCollectionItems
            });
    }
}
