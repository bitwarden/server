using System.Security.Claims;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Enums;
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
    [BitAutoData(OrganizationUserType.User, false, true)]
    [BitAutoData(OrganizationUserType.Admin, false, false)]
    [BitAutoData(OrganizationUserType.Owner, false, false)]
    [BitAutoData(OrganizationUserType.Custom, true, false)]
    [BitAutoData(OrganizationUserType.Owner, true, true)]
    public async Task CanReadAllAccessAsync_Success(
        OrganizationUserType userType, bool editAnyCollection, bool deleteAnyCollection,
        Guid userId, SutProvider<CollectionAuthorizationHandler> sutProvider,
        CurrentContextOrganization organization)
    {
        // if (org.Type is OrganizationUserType.Owner or OrganizationUserType.Admin ||
        //     org.Permissions.ManageGroups ||
        //     org.Permissions.ManageUsers ||
        //     org.Permissions.EditAnyCollection ||
        //     org.Permissions.DeleteAnyCollection ||
        //     org.Permissions.AccessImportExport ||
        //     await _currentContext.ProviderUserForOrgAsync(org.Id))
        // {
        //     context.Succeed(requirement);
        // }

        organization.Type = userType;
        organization.Permissions.EditAnyCollection = editAnyCollection;
        organization.Permissions.DeleteAnyCollection = deleteAnyCollection;

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
    public async Task CanReadAllAccessAsync_WithProviderUser_Success(
        Guid userId,
        SutProvider<CollectionAuthorizationHandler> sutProvider, CurrentContextOrganization organization)
    {
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

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_MissingUserId_Failure(
        SutProvider<CollectionAuthorizationHandler> sutProvider)
    {
        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.Create },
            new ClaimsPrincipal(),
            null
        );

        // Simulate missing user id
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_MissingOrg_Failure(
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
        Assert.True(context.HasFailed);
    }
}
