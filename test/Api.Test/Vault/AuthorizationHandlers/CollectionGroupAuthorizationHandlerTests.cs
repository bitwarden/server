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
public class CollectionGroupAuthorizationHandlerTests
{
    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_WithFeatureFlagDisabled_DoesNotSucceed(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization,
        Guid actingUserId)
    {
        var resources = collections.ToList();

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.CollectionUserCollectionGroupAuthorizationHandlers)
            .Returns(false);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Create },
            new ClaimsPrincipal(),
            resources);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanCreate_WithAdminOrOwnerAndAllowAdminAccess_Succeeds(
        OrganizationUserType userType,
        Guid actingUserId,
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        organization.Type = userType;
        organization.Permissions = new Permissions();

        var resources = collections.ToList();

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, true);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Create },
            new ClaimsPrincipal(),
            resources);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreate_WithCustomUserManageGroupsAndAllowAdminAccess_Succeeds(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization,
        Guid actingUserId)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { ManageGroups = true };

        var resources = collections.ToList();

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, true);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Create },
            new ClaimsPrincipal(),
            resources);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreate_WithCustomUserManageGroupsAndNoAllowAdminAccess_DoesNotSucceed(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization,
        Guid actingUserId)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { ManageGroups = true };

        var resources = collections.ToList();

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, false);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Create },
            new ClaimsPrincipal(),
            resources);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreate_WithUserManagePermissionOnAllCollections_Succeeds(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
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

        var resources = collections.ToList();

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, false);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Create },
            new ClaimsPrincipal(),
            resources);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreate_WithNoOrgAccessAndNotProvider_DoesNotSucceed(
        Guid actingUserId,
        ICollection<Collection> collections,
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider)
    {
        var resources = collections.ToList();

        ArrangeFeatureFlag(sutProvider);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Create },
            new ClaimsPrincipal(),
            resources);

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
    public async Task CanUpdate_WithAdminOrOwnerAndAllowAdminAccess_Succeeds(
        OrganizationUserType userType,
        Guid actingUserId,
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        organization.Type = userType;
        organization.Permissions = new Permissions();

        var resources = collections.ToList();

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, true);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Update },
            new ClaimsPrincipal(),
            resources);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanUpdate_WithCustomUserManageGroupsAndAllowAdminAccess_Succeeds(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization,
        Guid actingUserId)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { ManageGroups = true };

        var resources = collections.ToList();

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, true);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Update },
            new ClaimsPrincipal(),
            resources);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanUpdate_WithUserManagePermissionOnAllCollections_Succeeds(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
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

        var resources = collections.ToList();

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, false);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Update },
            new ClaimsPrincipal(),
            resources);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanUpdate_WithNoOrgAccessAndNotProvider_DoesNotSucceed(
        Guid actingUserId,
        ICollection<Collection> collections,
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider)
    {
        var resources = collections.ToList();

        ArrangeFeatureFlag(sutProvider);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Update },
            new ClaimsPrincipal(),
            resources);

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
    public async Task CanDelete_WithAdminOrOwnerAndAllowAdminAccess_Succeeds(
        OrganizationUserType userType,
        Guid actingUserId,
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        organization.Type = userType;
        organization.Permissions = new Permissions();

        var resources = collections.ToList();

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, true);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Delete },
            new ClaimsPrincipal(),
            resources);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanDelete_WithCustomUserManageGroupsAndAllowAdminAccess_Succeeds(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization,
        Guid actingUserId)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { ManageGroups = true };

        var resources = collections.ToList();

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, true);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Delete },
            new ClaimsPrincipal(),
            resources);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanDelete_WithUserManagePermissionOnAllCollections_Succeeds(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
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

        var resources = collections.ToList();

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, false);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Delete },
            new ClaimsPrincipal(),
            resources);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanDelete_WithNoOrgAccessAndNotProvider_DoesNotSucceed(
        Guid actingUserId,
        ICollection<Collection> collections,
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider)
    {
        var resources = collections.ToList();

        ArrangeFeatureFlag(sutProvider);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Delete },
            new ClaimsPrincipal(),
            resources);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>())
            .Returns((CurrentContextOrganization)null);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>())
            .Returns(false);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_WithNoUserId_Fails(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections)
    {
        var resources = collections.ToList();

        ArrangeFeatureFlag(sutProvider);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Create },
            new ClaimsPrincipal(),
            resources);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_WithNullResources_Fails(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider)
    {
        ArrangeFeatureFlag(sutProvider);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(Guid.NewGuid());

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Create },
            new ClaimsPrincipal(),
            (IEnumerable<Collection>)null);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_WithProviderUser_Succeeds(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        Guid actingUserId)
    {
        var resources = collections.ToList();

        ArrangeFeatureFlag(sutProvider);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Create },
            new ClaimsPrincipal(),
            resources);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>())
            .Returns((CurrentContextOrganization)null);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>())
            .Returns(true);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_WithMultipleOrgs_ThrowsBadRequest(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        Guid actingUserId)
    {
        ArrangeFeatureFlag(sutProvider);

        var collection1 = new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() };
        var collection2 = new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() };
        var resources = new List<Collection> { collection1, collection2 };

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Create },
            new ClaimsPrincipal(),
            resources);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.HandleAsync(context));
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreate_WithCustomUserEditAnyCollection_Succeeds(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization,
        Guid actingUserId)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { EditAnyCollection = true };

        var resources = collections.ToList();

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, false);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Create },
            new ClaimsPrincipal(),
            resources);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanCreate_WithAdminOrOwnerAndNoAllowAdminAccess_DoesNotSucceed(
        OrganizationUserType userType,
        Guid actingUserId,
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        organization.Type = userType;
        organization.Permissions = new Permissions();

        var resources = collections.ToList();

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, false);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Create },
            new ClaimsPrincipal(),
            resources);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>())
            .Returns(false);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanUpdate_WithCustomUserManageGroupsAndNoAllowAdminAccess_DoesNotSucceed(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization,
        Guid actingUserId)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { ManageGroups = true };

        var resources = collections.ToList();

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, false);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Update },
            new ClaimsPrincipal(),
            resources);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanUpdate_WithAdminOrOwnerAndNoAllowAdminAccess_DoesNotSucceed(
        OrganizationUserType userType,
        Guid actingUserId,
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        organization.Type = userType;
        organization.Permissions = new Permissions();

        var resources = collections.ToList();

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, false);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Update },
            new ClaimsPrincipal(),
            resources);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>())
            .Returns(false);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanDelete_WithCustomUserManageGroupsAndNoAllowAdminAccess_DoesNotSucceed(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization,
        Guid actingUserId)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { ManageGroups = true };

        var resources = collections.ToList();

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, false);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Delete },
            new ClaimsPrincipal(),
            resources);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanDelete_WithAdminOrOwnerAndNoAllowAdminAccess_DoesNotSucceed(
        OrganizationUserType userType,
        Guid actingUserId,
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        organization.Type = userType;
        organization.Permissions = new Permissions();

        var resources = collections.ToList();

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, false);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Delete },
            new ClaimsPrincipal(),
            resources);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>())
            .Returns(false);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanCreate_WithOrphanedCollectionAndAdminRole_Succeeds(
        OrganizationUserType userType,
        Guid actingUserId,
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        organization.Type = userType;
        organization.Permissions = new Permissions();

        var resources = collections.ToList();

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, true);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Create },
            new ClaimsPrincipal(),
            resources);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId)
            .Returns(new List<CollectionDetails>());
        ArrangeOrphanedCollections(sutProvider, organization.Id, collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreate_WithOrphanedCollectionAndUserRole_DoesNotSucceed(
        Guid actingUserId,
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        var resources = collections.ToList();

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, false);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Create },
            new ClaimsPrincipal(),
            resources);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId)
            .Returns(new List<CollectionDetails>());
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>())
            .Returns(false);
        ArrangeOrphanedCollections(sutProvider, organization.Id, collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreate_WithOrphanedCollectionAndEditAnyCollectionPermission_Succeeds(
        Guid actingUserId,
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { EditAnyCollection = true };

        var resources = collections.ToList();

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, false);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Create },
            new ClaimsPrincipal(),
            resources);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanUpdate_WithOrphanedCollectionAndAdminRole_Succeeds(
        OrganizationUserType userType,
        Guid actingUserId,
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        organization.Type = userType;
        organization.Permissions = new Permissions();

        var resources = collections.ToList();

        ArrangeFeatureFlag(sutProvider);
        ArrangeOrganizationAbility(sutProvider, organization, true);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Update },
            new ClaimsPrincipal(),
            resources);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId)
            .Returns(new List<CollectionDetails>());
        ArrangeOrphanedCollections(sutProvider, organization.Id, collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    private static void ArrangeFeatureFlag(SutProvider<CollectionGroupAuthorizationHandler> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.CollectionUserCollectionGroupAuthorizationHandlers)
            .Returns(true);
    }

    private static void ArrangeOrganizationAbility(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
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

    private static void ArrangeOrphanedCollections(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        Guid organizationId,
        ICollection<Collection> orphanedCollections)
    {
        var orgCollections = orphanedCollections
            .Select(c => new Tuple<Collection, CollectionAccessDetails>(
                c,
                new CollectionAccessDetails
                {
                    Users = new List<CollectionAccessSelection>(),
                    Groups = new List<CollectionAccessSelection>()
                }))
            .ToList();

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByOrganizationIdWithAccessAsync(organizationId)
            .Returns(orgCollections);
    }
}
