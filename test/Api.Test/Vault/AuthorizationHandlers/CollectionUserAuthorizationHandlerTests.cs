using System.Security.Claims;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
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

namespace Bit.Api.Test.Vault.AuthorizationHandlers;

[SutProviderCustomize]
public class CollectionUserAuthorizationHandlerTests
{
    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_WithFeatureFlagDisabled_DoesNotSucceed(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
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
            MakeResource(collections));

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanCreate_WhenAdminOrOwner_Success(
        OrganizationUserType userType,
        Guid actingUserId,
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
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
            MakeResource(collections));

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreate_CustomUserWithManageUsers_WhenAdminAccessToAllCollectionItemsEnabled_Success(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
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
            MakeResource(collections));

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreate_CustomUserWithManageUsers_WhenAdminAccessToAllCollectionItemsDisabled_Failure(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization,
        Guid actingUserId)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { ManageUsers = true };

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, false);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperations.Create },
            new ClaimsPrincipal(),
            MakeResource(collections));

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreate_UserWithCollectionManagePermission_Success(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
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
            MakeResource(collections));

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreate_WhenMissingOrgAccess_NoSuccess(
        Guid actingUserId,
        ICollection<Collection> collections,
        SutProvider<CollectionUserAuthorizationHandler> sutProvider)
    {
        ArrangeFeatureFlag(sutProvider);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperations.Create },
            new ClaimsPrincipal(),
            MakeResource(collections));

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>())
            .Returns((CurrentContextOrganization)null);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>())
            .Returns(false);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanCreate_SelfAssignment_WithAllowAdminAccess_Succeeds(
        OrganizationUserType userType,
        Guid actingUserId,
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
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
            MakeResource(collections, targetUserId: actingUserId));

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanCreate_SelfAssignment_WithoutAllowAdminAccess_Fails(
        OrganizationUserType userType,
        Guid actingUserId,
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        organization.Type = userType;
        organization.Permissions = new Permissions();

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, false);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperations.Create },
            new ClaimsPrincipal(),
            MakeResource(collections, targetUserId: actingUserId));

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanCreate_NotSelf_Succeeds(
        OrganizationUserType userType,
        Guid actingUserId,
        Guid targetUserId,
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
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
            MakeResource(collections, targetUserId: targetUserId));

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanUpdate_WhenAdminOrOwner_Success(
        OrganizationUserType userType,
        Guid actingUserId,
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        organization.Type = userType;
        organization.Permissions = new Permissions();

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, true);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperations.Update },
            new ClaimsPrincipal(),
            MakeResource(collections));

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanUpdate_CustomUserWithManageUsers_WhenAdminAccessToAllCollectionItemsEnabled_Success(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization,
        Guid actingUserId)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { ManageUsers = true };

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, true);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperations.Update },
            new ClaimsPrincipal(),
            MakeResource(collections));

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanUpdate_CustomUserWithManageUsers_WhenAdminAccessToAllCollectionItemsDisabled_Failure(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization,
        Guid actingUserId)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { ManageUsers = true };

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, false);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperations.Update },
            new ClaimsPrincipal(),
            MakeResource(collections));

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanUpdate_UserWithCollectionManagePermission_Success(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
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
            new[] { CollectionUserOperations.Update },
            new ClaimsPrincipal(),
            MakeResource(collections));

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanUpdate_WhenMissingOrgAccess_NoSuccess(
        Guid actingUserId,
        ICollection<Collection> collections,
        SutProvider<CollectionUserAuthorizationHandler> sutProvider)
    {
        ArrangeFeatureFlag(sutProvider);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperations.Update },
            new ClaimsPrincipal(),
            MakeResource(collections));

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>())
            .Returns((CurrentContextOrganization)null);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>())
            .Returns(false);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanDelete_WhenAdminOrOwner_Success(
        OrganizationUserType userType,
        Guid actingUserId,
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        organization.Type = userType;
        organization.Permissions = new Permissions();

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, true);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperations.Delete },
            new ClaimsPrincipal(),
            MakeResource(collections));

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanDelete_CustomUserWithManageUsers_WhenAdminAccessToAllCollectionItemsEnabled_Success(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization,
        Guid actingUserId)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { ManageUsers = true };

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, true);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperations.Delete },
            new ClaimsPrincipal(),
            MakeResource(collections));

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanDelete_UserWithCollectionManagePermission_Success(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
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
            new[] { CollectionUserOperations.Delete },
            new ClaimsPrincipal(),
            MakeResource(collections));

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanDelete_WhenMissingOrgAccess_NoSuccess(
        Guid actingUserId,
        ICollection<Collection> collections,
        SutProvider<CollectionUserAuthorizationHandler> sutProvider)
    {
        ArrangeFeatureFlag(sutProvider);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperations.Delete },
            new ClaimsPrincipal(),
            MakeResource(collections));

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>())
            .Returns((CurrentContextOrganization)null);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>())
            .Returns(false);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_MissingUserId_Failure(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
        ICollection<Collection> collections)
    {
        ArrangeFeatureFlag(sutProvider);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperations.Create },
            new ClaimsPrincipal(),
            MakeResource(collections));

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_NullCollections_Failure(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider)
    {
        ArrangeFeatureFlag(sutProvider);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(Guid.NewGuid());

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperations.Create },
            new ClaimsPrincipal(),
            new CollectionUserAccessResource(null!, null));

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_Provider_Success(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        Guid actingUserId)
    {
        ArrangeFeatureFlag(sutProvider);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperations.Create },
            new ClaimsPrincipal(),
            MakeResource(collections));

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>())
            .Returns((CurrentContextOrganization)null);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>())
            .Returns(true);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_MultipleOrgs_Exception(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
        Guid actingUserId)
    {
        ArrangeFeatureFlag(sutProvider);

        var collection1 = new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() };
        var collection2 = new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() };
        var resource = new CollectionUserAccessResource(new List<Collection> { collection1, collection2 }, null);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperations.Create },
            new ClaimsPrincipal(),
            resource);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.HandleAsync(context));
    }

    private static CollectionUserAccessResource MakeResource<T>(
        ICollection<T> collections, Guid? targetUserId = null) where T : Collection
    {
        return new CollectionUserAccessResource(collections.Cast<Collection>().ToList(), targetUserId);
    }

    private static void ArrangeFeatureFlag(SutProvider<CollectionUserAuthorizationHandler> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.CollectionUserCollectionGroupAuthorizationHandlers)
            .Returns(true);
    }

    private static void ArrangeOrganizationAbility(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
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
