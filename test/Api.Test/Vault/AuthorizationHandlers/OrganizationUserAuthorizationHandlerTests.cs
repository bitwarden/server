using System.Security.Claims;
using Bit.Api.Vault.AuthorizationHandlers.OrganizationUsers;
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
public class OrganizationUserAuthorizationHandlerTests
{
    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanReadAllAsync_WhenAdminOrOwner_Success(
        OrganizationUserType userType,
        Guid userId, SutProvider<OrganizationUserAuthorizationHandler> sutProvider,
        CurrentContextOrganization organization)
    {
        organization.Type = userType;
        organization.Permissions = new Permissions();

        var context = new AuthorizationHandlerContext(
            new[] { OrganizationUserOperations.ReadAll(organization.Id) },
            new ClaimsPrincipal(),
            null);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task CanReadAllAsync_WithProviderUser_Success(
        Guid userId,
        SutProvider<OrganizationUserAuthorizationHandler> sutProvider, CurrentContextOrganization organization)
    {
        var context = new AuthorizationHandlerContext(
            new[] { OrganizationUserOperations.ReadAll(organization.Id) },
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
    [BitAutoData(true, false, false, false)]
    [BitAutoData(false, true, false, false)]
    [BitAutoData(false, false, true, false)]
    [BitAutoData(false, false, false, true)]
    public async Task CanReadAllAsync_WhenCustomUserWithRequiredPermissions_Success(
        bool editAnyCollection, bool deleteAnyCollection, bool manageGroups, bool manageUsers,
        SutProvider<OrganizationUserAuthorizationHandler> sutProvider,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions
        {
            EditAnyCollection = editAnyCollection,
            DeleteAnyCollection = deleteAnyCollection,
            ManageGroups = manageGroups,
            ManageUsers = manageUsers
        };

        var context = new AuthorizationHandlerContext(
            new[] { OrganizationUserOperations.ReadAll(organization.Id) },
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
    public async Task CanReadAllAsync_WhenMissingAccess_Failure(
        OrganizationUserType userType,
        SutProvider<OrganizationUserAuthorizationHandler> sutProvider,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = userType;
        organization.LimitCollectionCreationDeletion = true;
        organization.Permissions = new Permissions
        {
            EditAnyCollection = false,
            DeleteAnyCollection = false,
            ManageGroups = false,
            ManageUsers = false,
            AccessImportExport = false
        };

        var context = new AuthorizationHandlerContext(
            new[] { OrganizationUserOperations.ReadAll(organization.Id) },
            new ClaimsPrincipal(),
            null);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_MissingUserId_Failure(
        Guid organizationId,
        SutProvider<OrganizationUserAuthorizationHandler> sutProvider)
    {
        var context = new AuthorizationHandlerContext(
            new[] { OrganizationUserOperations.ReadAll(organizationId) },
            new ClaimsPrincipal(),
            null
        );

        // Simulate missing user id
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        await sutProvider.Sut.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_MissingOrg_Failure(
        Guid userId,
        Guid organizationId,
        SutProvider<OrganizationUserAuthorizationHandler> sutProvider)
    {
        var context = new AuthorizationHandlerContext(
            new[] { OrganizationUserOperations.ReadAll(organizationId) },
            new ClaimsPrincipal(),
            null
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>()).Returns((CurrentContextOrganization)null);

        await sutProvider.Sut.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_NoSpecifiedOrgId_NoSuccessOrFailure(
        SutProvider<OrganizationUserAuthorizationHandler> sutProvider)
    {
        var context = new AuthorizationHandlerContext(
            new[] { OrganizationUserOperations.ReadAll(default) },
            new ClaimsPrincipal(),
            null
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(new Guid());

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.False(context.HasFailed);
    }
}
