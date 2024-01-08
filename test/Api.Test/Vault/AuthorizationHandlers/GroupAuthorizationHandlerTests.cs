using System.Security.Claims;
using Bit.Api.Vault.AuthorizationHandlers.Groups;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Vault.AuthorizationHandlers;

[SutProviderCustomize]
public class GroupAuthorizationHandlerTests
{
    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanReadAllAsync_WhenAdminOrOwner_Success(
        OrganizationUserType userType,
        Guid userId, SutProvider<GroupAuthorizationHandler> sutProvider,
        CurrentContextOrganization organization)
    {
        organization.Type = userType;
        organization.Permissions = new Permissions();

        var organizationAbilities = ArrangeOrganizationAbilitiesDictionary(organization.Id, true);

        var context = new AuthorizationHandlerContext(
            new[] { GroupOperations.ReadAll(organization.Id) },
            new ClaimsPrincipal(),
            null);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilitiesAsync().Returns(organizationAbilities);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task CanReadAllAsync_WithProviderUser_Success(
        Guid userId,
        SutProvider<GroupAuthorizationHandler> sutProvider, CurrentContextOrganization organization)
    {
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        var organizationAbilities = ArrangeOrganizationAbilitiesDictionary(organization.Id, true);

        var context = new AuthorizationHandlerContext(
            new[] { GroupOperations.ReadAll(organization.Id) },
            new ClaimsPrincipal(),
            null);

        sutProvider.GetDependency<ICurrentContext>()
            .UserId
            .Returns(userId);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilitiesAsync().Returns(organizationAbilities);
        sutProvider.GetDependency<ICurrentContext>()
            .ProviderUserForOrgAsync(organization.Id)
            .Returns(true);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData(true, false, false, false, true)]
    [BitAutoData(false, true, false, false, true)]
    [BitAutoData(false, false, true, false, true)]
    [BitAutoData(false, false, false, true, true)]
    [BitAutoData(false, false, false, false, false)]
    public async Task CanReadAllAsync_WhenCustomUserWithRequiredPermissions_Success(
        bool editAnyCollection, bool deleteAnyCollection, bool manageGroups,
        bool manageUsers, bool limitCollectionCreationDeletion,
        SutProvider<GroupAuthorizationHandler> sutProvider,
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

        var organizationAbilities = ArrangeOrganizationAbilitiesDictionary(organization.Id, limitCollectionCreationDeletion);

        var context = new AuthorizationHandlerContext(
            new[] { GroupOperations.ReadAll(organization.Id) },
            new ClaimsPrincipal(),
            null);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilitiesAsync().Returns(organizationAbilities);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task CanReadAllAsync_WhenMissingPermissions_NoSuccess(
        OrganizationUserType userType,
        SutProvider<GroupAuthorizationHandler> sutProvider,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = userType;
        organization.Permissions = new Permissions
        {
            EditAnyCollection = false,
            DeleteAnyCollection = false,
            ManageGroups = false,
            ManageUsers = false,
            AccessImportExport = false
        };

        var organizationAbilities = ArrangeOrganizationAbilitiesDictionary(organization.Id, true);

        var context = new AuthorizationHandlerContext(
            new[] { GroupOperations.ReadAll(organization.Id) },
            new ClaimsPrincipal(),
            null);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilitiesAsync().Returns(organizationAbilities);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task CanReadAllAsync_WhenMissingOrgAccess_NoSuccess(
        Guid userId,
        Guid organizationId,
        SutProvider<GroupAuthorizationHandler> sutProvider)
    {
        var organizationAbilities = ArrangeOrganizationAbilitiesDictionary(organizationId, true);

        var context = new AuthorizationHandlerContext(
            new[] { GroupOperations.ReadAll(organizationId) },
            new ClaimsPrincipal(),
            null
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>()).Returns((CurrentContextOrganization)null);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilitiesAsync().Returns(organizationAbilities);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        await sutProvider.Sut.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_MissingUserId_Failure(
        Guid organizationId,
        SutProvider<GroupAuthorizationHandler> sutProvider)
    {
        var context = new AuthorizationHandlerContext(
            new[] { GroupOperations.ReadAll(organizationId) },
            new ClaimsPrincipal(),
            null
        );

        // Simulate missing user id
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        await sutProvider.Sut.HandleAsync(context);
        Assert.False(context.HasSucceeded);
        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_NoSpecifiedOrgId_Failure(
        SutProvider<GroupAuthorizationHandler> sutProvider)
    {
        var context = new AuthorizationHandlerContext(
            new[] { GroupOperations.ReadAll(default) },
            new ClaimsPrincipal(),
            null
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(new Guid());

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.True(context.HasFailed);
    }

    private static Dictionary<Guid, OrganizationAbility> ArrangeOrganizationAbilitiesDictionary(Guid orgId,
        bool limitCollectionCreationDeletion)
    {
        return new Dictionary<Guid, OrganizationAbility>
        {
            { orgId,
                new OrganizationAbility
                {
                    LimitCollectionCreationDeletion = limitCollectionCreationDeletion
                }
            }
        };
    }
}
