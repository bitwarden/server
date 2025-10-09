using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization;

[SutProviderCustomize]
public class RecoverProviderAccountAuthorizationHandlerTests
{
    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_UserIdIsNull_Blocks(
        SutProvider<RecoverProviderAccountAuthorizationHandler> sutProvider,
        OrganizationUser targetOrganizationUser,
        ClaimsPrincipal claimsPrincipal)
    {
        // Arrange
        targetOrganizationUser.UserId = null;

        var context = new AuthorizationHandlerContext(
            [new RecoverAccountAuthorizationRequirement()],
            claimsPrincipal,
            targetOrganizationUser);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_TargetUserIsNotProviderUser_DoesNotBlock(
        SutProvider<RecoverProviderAccountAuthorizationHandler> sutProvider,
        [OrganizationUser] OrganizationUser targetOrganizationUser,
        ClaimsPrincipal claimsPrincipal)
    {
        // Arrange
        var context = new AuthorizationHandlerContext(
            [new RecoverAccountAuthorizationRequirement()],
            claimsPrincipal,
            targetOrganizationUser);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByUserAsync(targetOrganizationUser.UserId!.Value)
            .Returns(Array.Empty<ProviderUser>());

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
        Assert.False(context.HasFailed);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_CurrentUserIsMemberOfAllTargetUserProviders_DoesNotBlock(
        SutProvider<RecoverProviderAccountAuthorizationHandler> sutProvider,
        [OrganizationUser] OrganizationUser targetOrganizationUser,
        ClaimsPrincipal claimsPrincipal,
        Guid providerId1,
        Guid providerId2)
    {
        // Arrange
        var targetUserProviders = new List<ProviderUser>
        {
            new() { ProviderId = providerId1, UserId = targetOrganizationUser.UserId },
            new() { ProviderId = providerId2, UserId = targetOrganizationUser.UserId }
        };

        var context = new AuthorizationHandlerContext(
            [new RecoverAccountAuthorizationRequirement()],
            claimsPrincipal,
            targetOrganizationUser);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByUserAsync(targetOrganizationUser.UserId!.Value)
            .Returns(targetUserProviders);

        sutProvider.GetDependency<ICurrentContext>()
            .ProviderUser(providerId1)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>()
            .ProviderUser(providerId2)
            .Returns(true);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
        Assert.False(context.HasFailed);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_CurrentUserMissingProviderMembership_Blocks(
        SutProvider<RecoverProviderAccountAuthorizationHandler> sutProvider,
        [OrganizationUser] OrganizationUser targetOrganizationUser,
        ClaimsPrincipal claimsPrincipal,
        Guid providerId1,
        Guid providerId2)
    {
        // Arrange
        var targetUserProviders = new List<ProviderUser>
        {
            new() { ProviderId = providerId1, UserId = targetOrganizationUser.UserId },
            new() { ProviderId = providerId2, UserId = targetOrganizationUser.UserId }
        };

        var context = new AuthorizationHandlerContext(
            [new RecoverAccountAuthorizationRequirement()],
            claimsPrincipal,
            targetOrganizationUser);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByUserAsync(targetOrganizationUser.UserId!.Value)
            .Returns(targetUserProviders);

        sutProvider.GetDependency<ICurrentContext>()
            .ProviderUser(providerId1)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>()
            .ProviderUser(providerId2)
            .Returns(false);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
        Assert.True(context.HasFailed);
    }
}
