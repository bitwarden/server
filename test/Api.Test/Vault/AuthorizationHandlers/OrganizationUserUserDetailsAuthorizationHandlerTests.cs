using System.Security.Claims;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Vault.AuthorizationHandlers;

[SutProviderCustomize]
public class OrganizationUserUserDetailsAuthorizationHandlerTests
{
    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task CanReadAllAsync_WhenMemberOfOrg_Success(
        OrganizationUserType userType,
        Guid userId, SutProvider<OrganizationUserUserDetailsAuthorizationHandler> sutProvider,
        CurrentContextOrganization organization)
    {
        organization.Type = userType;
        organization.Permissions = new Permissions();

        var context = new AuthorizationHandlerContext(
            new[] { OrganizationUserUserDetailsOperations.ReadAll(organization.Id) },
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
        SutProvider<OrganizationUserUserDetailsAuthorizationHandler> sutProvider, CurrentContextOrganization organization)
    {
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        var context = new AuthorizationHandlerContext(
            new[] { OrganizationUserUserDetailsOperations.ReadAll(organization.Id) },
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
    public async Task HandleRequirementAsync_WhenMissingOrgAccess_NoSuccess(
        Guid userId,
        CurrentContextOrganization organization,
        SutProvider<OrganizationUserUserDetailsAuthorizationHandler> sutProvider)
    {
        var context = new AuthorizationHandlerContext(
            new[] { OrganizationUserUserDetailsOperations.ReadAll(organization.Id) },
            new ClaimsPrincipal(),
            null
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>()).Returns((CurrentContextOrganization)null);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        await sutProvider.Sut.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_MissingUserId_Failure(
        Guid organizationId,
        SutProvider<OrganizationUserUserDetailsAuthorizationHandler> sutProvider)
    {
        var context = new AuthorizationHandlerContext(
            new[] { OrganizationUserUserDetailsOperations.ReadAll(organizationId) },
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
        SutProvider<OrganizationUserUserDetailsAuthorizationHandler> sutProvider)
    {
        var context = new AuthorizationHandlerContext(
            new[] { OrganizationUserUserDetailsOperations.ReadAll(default) },
            new ClaimsPrincipal(),
            null
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(new Guid());

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasFailed);
    }
}
