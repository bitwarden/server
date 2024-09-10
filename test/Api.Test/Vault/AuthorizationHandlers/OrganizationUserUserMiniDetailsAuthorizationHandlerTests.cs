using System.Security.Claims;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Services;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Vault.AuthorizationHandlers;

[SutProviderCustomize]
public class OrganizationUserUserMiniDetailsAuthorizationHandlerTests
{
    [Theory, CurrentContextOrganizationCustomize]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task ReadAll_Admins_Success(
        OrganizationUserType userType,
        CurrentContextOrganization organization,
        SutProvider<OrganizationUserUserMiniDetailsAuthorizationHandler> sutProvider)
    {
        organization.Type = userType;
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        var context = new AuthorizationHandlerContext(
            new[] { OrganizationUserUserMiniDetailsOperations.ReadAll },
            new ClaimsPrincipal(),
            new OrganizationIdResource(organization.Id));

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CurrentContextOrganizationCustomize]
    [BitAutoData(OrganizationUserType.User)]
    public async Task ReadAll_Member_CanCreateCollections_Success(
        OrganizationUserType userType,
        CurrentContextOrganization organization,
        SutProvider<OrganizationUserUserMiniDetailsAuthorizationHandler> sutProvider)
    {
        organization.Type = userType;
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility { LimitCollectionCreationDeletion = false });

        var context = new AuthorizationHandlerContext(
            new[] { OrganizationUserUserMiniDetailsOperations.ReadAll },
            new ClaimsPrincipal(),
            new OrganizationIdResource(organization.Id));

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CurrentContextOrganizationCustomize]
    [BitAutoData(OrganizationUserType.User)]
    public async Task ReadAll_Member_CannotCreateCollections_NoSuccess(
        OrganizationUserType userType,
        CurrentContextOrganization organization,
        SutProvider<OrganizationUserUserMiniDetailsAuthorizationHandler> sutProvider)
    {
        organization.Type = userType;
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility { LimitCollectionCreationDeletion = true });

        var context = new AuthorizationHandlerContext(
            new[] { OrganizationUserUserMiniDetailsOperations.ReadAll },
            new ClaimsPrincipal(),
            new OrganizationIdResource(organization.Id));

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CurrentContextOrganizationCustomize]
    public async Task ReadAll_ProviderUser_Success(
        CurrentContextOrganization organization,
        SutProvider<OrganizationUserUserMiniDetailsAuthorizationHandler> sutProvider)
    {
        organization.Type = OrganizationUserType.User;
        sutProvider.GetDependency<ICurrentContext>()
            .ProviderUserForOrgAsync(organization.Id)
            .Returns(true);

        var context = new AuthorizationHandlerContext(
            new[] { OrganizationUserUserMiniDetailsOperations.ReadAll },
            new ClaimsPrincipal(),
            new OrganizationIdResource(organization.Id));

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CurrentContextOrganizationCustomize]
    public async Task ReadAll_User_NoSuccess(
        CurrentContextOrganization organization,
        SutProvider<OrganizationUserUserMiniDetailsAuthorizationHandler> sutProvider)
    {
        organization.Type = OrganizationUserType.User;
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>()).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        var context = new AuthorizationHandlerContext(
            new[] { OrganizationUserUserMiniDetailsOperations.ReadAll },
            new ClaimsPrincipal(),
            new OrganizationIdResource(organization.Id)
        );

        await sutProvider.Sut.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CurrentContextOrganizationCustomize]
    public async Task ReadAll_NotMember_NoSuccess(
        CurrentContextOrganization organization,
        SutProvider<OrganizationUserUserMiniDetailsAuthorizationHandler> sutProvider)
    {
        var context = new AuthorizationHandlerContext(
            new[] { OrganizationUserUserMiniDetailsOperations.ReadAll },
            new ClaimsPrincipal(),
            new OrganizationIdResource(organization.Id)
        );

        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>()).Returns((CurrentContextOrganization)null);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        await sutProvider.Sut.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }
}
