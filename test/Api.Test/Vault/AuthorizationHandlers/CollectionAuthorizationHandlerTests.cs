using System.Security.Claims;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Test.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Vault.AuthorizationHandlers;

[SutProviderCustomize]
[FeatureServiceCustomize(FeatureFlagKeys.FlexibleCollections)]
public class CollectionAuthorizationHandlerTests
{
    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanReadAllAsync_WhenAdminOrOwner_Success(
        OrganizationUserType userType,
        Guid userId, SutProvider<CollectionAuthorizationHandler> sutProvider,
        CurrentContextOrganization organization)
    {
        organization.Type = userType;

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.ReadAll(organization.Id) },
            new ClaimsPrincipal(),
            null);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task CanReadAllAsync_WhenProviderUser_Success(
        Guid userId,
        SutProvider<CollectionAuthorizationHandler> sutProvider, CurrentContextOrganization organization)
    {
        organization.Type = OrganizationUserType.User;

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.ReadAll(organization.Id) },
            new ClaimsPrincipal(),
            null);

        sutProvider.GetDependency<ICurrentContext>()
            .UserId
            .Returns(userId);
        sutProvider.GetDependency<ICurrentContext>()
            .ProviderUserForOrgAsync(organization.Id)
            .Returns(true);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData(true, false, false)]
    [BitAutoData(false, true, false)]
    [BitAutoData(false, false, true)]
    public async Task CanReadAllAsync_WhenCustomUserWithRequiredPermissions_Success(
        bool editAnyCollection, bool deleteAnyCollection, bool accessImportExport,
        SutProvider<CollectionAuthorizationHandler> sutProvider,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions
        {
            EditAnyCollection = editAnyCollection,
            DeleteAnyCollection = deleteAnyCollection,
            AccessImportExport = accessImportExport
        };

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.ReadAll(organization.Id) },
            new ClaimsPrincipal(),
            null);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task CanReadAllAsync_WhenMissingPermissions_NoSuccess(
        OrganizationUserType userType,
        SutProvider<CollectionAuthorizationHandler> sutProvider,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = userType;
        organization.Permissions = new Permissions
        {
            EditAnyCollection = false,
            DeleteAnyCollection = false,
            AccessImportExport = false
        };

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.ReadAll(organization.Id) },
            new ClaimsPrincipal(),
            null);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanReadAllWithAccessAsync_WhenAdminOrOwner_Success(
        OrganizationUserType userType,
        Guid userId, SutProvider<CollectionAuthorizationHandler> sutProvider,
        CurrentContextOrganization organization)
    {
        organization.Type = userType;

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.ReadAllWithAccess(organization.Id) },
            new ClaimsPrincipal(),
            null);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task CanReadAllWithAccessAsync_WhenProviderUser_Success(
        Guid userId,
        SutProvider<CollectionAuthorizationHandler> sutProvider, CurrentContextOrganization organization)
    {
        organization.Type = OrganizationUserType.User;

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.ReadAllWithAccess(organization.Id) },
            new ClaimsPrincipal(),
            null);

        sutProvider.GetDependency<ICurrentContext>()
            .UserId
            .Returns(userId);
        sutProvider.GetDependency<ICurrentContext>()
            .ProviderUserForOrgAsync(organization.Id)
            .Returns(true);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData(true, false)]
    [BitAutoData(false, true)]
    public async Task CanReadAllWithAccessAsync_WhenCustomUserWithRequiredPermissions_Success(
        bool editAnyCollection, bool deleteAnyCollection,
        SutProvider<CollectionAuthorizationHandler> sutProvider,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions
        {
            EditAnyCollection = editAnyCollection,
            DeleteAnyCollection = deleteAnyCollection,
        };

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.ReadAllWithAccess(organization.Id) },
            new ClaimsPrincipal(),
            null);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task CanReadAllWithAccessAsync_WhenMissingPermissions_NoSuccess(
        OrganizationUserType userType,
        SutProvider<CollectionAuthorizationHandler> sutProvider,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = userType;
        organization.Permissions = new Permissions
        {
            EditAnyCollection = false,
            DeleteAnyCollection = false,
            AccessImportExport = false
        };

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.ReadAllWithAccess(organization.Id) },
            new ClaimsPrincipal(),
            null);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanReadManyWithDetailsAsync_WhenAdminOrOwner_Success(
        OrganizationUserType userType,
        Guid userId, SutProvider<CollectionAuthorizationHandler> sutProvider,
        CurrentContextOrganization organization)
    {
        organization.Type = userType;

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.ReadManyWithDetails(organization.Id) },
            new ClaimsPrincipal(),
            null);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task CanReadManyWithDetailsAsync_WhenProviderUser_Success(
        Guid userId,
        SutProvider<CollectionAuthorizationHandler> sutProvider, CurrentContextOrganization organization)
    {
        organization.Type = OrganizationUserType.User;

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.ReadManyWithDetails(organization.Id) },
            new ClaimsPrincipal(),
            null);

        sutProvider.GetDependency<ICurrentContext>()
            .UserId
            .Returns(userId);
        sutProvider.GetDependency<ICurrentContext>()
            .ProviderUserForOrgAsync(organization.Id)
            .Returns(true);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData(true, false, false, false, false, true)]
    [BitAutoData(false, true, false, false, false, true)]
    [BitAutoData(false, false, true, false, false, true)]
    [BitAutoData(false, false, false, true, false, true)]
    [BitAutoData(false, false, false, false, true, true)]
    [BitAutoData(false, false, false, false, false, false)]
    public async Task CanReadManyWithDetailsAsync_WhenCustomUserWithRequiredPermissions_Success(
        bool editAnyCollection, bool deleteAnyCollection, bool createNewCollections,
        bool manageUsers, bool manageGroups, bool limitCollectionCreationDeletion,
        SutProvider<CollectionAuthorizationHandler> sutProvider,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.LimitCollectionCreationDeletion = limitCollectionCreationDeletion;
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions
        {
            EditAnyCollection = editAnyCollection,
            DeleteAnyCollection = deleteAnyCollection,
            CreateNewCollections = createNewCollections,
            ManageUsers = manageUsers,
            ManageGroups = manageGroups
        };

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.ReadManyWithDetails(organization.Id) },
            new ClaimsPrincipal(),
            null);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task CanReadManyWithDetailsAsync_WhenMissingPermissions_NoSuccess(
        OrganizationUserType userType,
        SutProvider<CollectionAuthorizationHandler> sutProvider,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.LimitCollectionCreationDeletion = true;
        organization.Type = userType;
        organization.Permissions = new Permissions();

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.ReadManyWithDetails(organization.Id) },
            new ClaimsPrincipal(),
            null);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_WhenMissingOrgAccess_NoSuccess(
        Guid userId,
        Guid organizationId,
        SutProvider<CollectionAuthorizationHandler> sutProvider)
    {
        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.ReadAll(organizationId) },
            new ClaimsPrincipal(),
            null
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>()).Returns((CurrentContextOrganization)null);

        await sutProvider.Sut.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_MissingUserId_Failure(
        Guid organizationId,
        SutProvider<CollectionAuthorizationHandler> sutProvider)
    {
        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.ReadAll(organizationId) },
            new ClaimsPrincipal(),
            null
        );

        // Simulate missing user id
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_NoSpecifiedOrgId_Failure(
        SutProvider<CollectionAuthorizationHandler> sutProvider)
    {
        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.ReadAll(default) },
            new ClaimsPrincipal(),
            null
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(new Guid());

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasFailed);
    }
}
