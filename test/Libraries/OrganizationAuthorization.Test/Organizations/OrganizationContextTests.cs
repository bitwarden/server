using System.Security.Claims;
using AutoFixture;
using Bit.Api.AdminConsole.Authorization;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization;

[SutProviderCustomize]
public class OrganizationContextTests
{
    [Theory, BitAutoData]
    public async Task IsProviderUserForOrganization_UserIsProviderUser_ReturnsTrue(
        Guid userId, Guid organizationId, Guid otherOrganizationId,
        SutProvider<OrganizationContext> sutProvider)
    {
        var claimsPrincipal = new ClaimsPrincipal();
        var providerUserOrganizations = new List<ProviderUserOrganizationDetails>
        {
            new() { OrganizationId = organizationId },
            new() { OrganizationId = otherOrganizationId }
        };

        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(claimsPrincipal)
            .Returns(userId);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyOrganizationDetailsByUserAsync(userId, ProviderUserStatusType.Confirmed)
            .Returns(providerUserOrganizations);

        var result = await sutProvider.Sut.IsProviderUserForOrganization(claimsPrincipal, organizationId);

        Assert.True(result);
        await sutProvider.GetDependency<IProviderUserRepository>()
            .Received(1)
            .GetManyOrganizationDetailsByUserAsync(userId, ProviderUserStatusType.Confirmed);
    }

    public static IEnumerable<object[]> UserIsNotProviderUserData()
    {
        // User has provider organizations, but not for the target organization
        yield return
        [
            new List<ProviderUserOrganizationDetails>
            {
                new Fixture().Create<ProviderUserOrganizationDetails>()
            }
        ];

        // User has no provider organizations
        yield return [Array.Empty<ProviderUserOrganizationDetails>()];
    }

    [Theory, BitMemberAutoData(nameof(UserIsNotProviderUserData))]
    public async Task IsProviderUserForOrganization_UserIsNotProviderUser_ReturnsFalse(
        IEnumerable<ProviderUserOrganizationDetails> providerUserOrganizations,
        Guid userId, Guid organizationId,
        SutProvider<OrganizationContext> sutProvider)
    {
        var claimsPrincipal = new ClaimsPrincipal();

        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(claimsPrincipal)
            .Returns(userId);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyOrganizationDetailsByUserAsync(userId, ProviderUserStatusType.Confirmed)
            .Returns(providerUserOrganizations);

        var result = await sutProvider.Sut.IsProviderUserForOrganization(claimsPrincipal, organizationId);

        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task IsProviderUserForOrganization_UserIdIsNull_ThrowsException(
        Guid organizationId,
        SutProvider<OrganizationContext> sutProvider)
    {
        var claimsPrincipal = new ClaimsPrincipal();

        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(claimsPrincipal)
            .Returns((Guid?)null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sutProvider.Sut.IsProviderUserForOrganization(claimsPrincipal, organizationId));

        Assert.Equal(OrganizationContext.NoUserIdError, exception.Message);
    }

    [Theory, BitAutoData]
    public async Task IsProviderUserForOrganization_UsesCaching(
        Guid userId, Guid organizationId,
        SutProvider<OrganizationContext> sutProvider)
    {
        var claimsPrincipal = new ClaimsPrincipal();
        var providerUserOrganizations = new List<ProviderUserOrganizationDetails>
        {
            new() { OrganizationId = organizationId }
        };

        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(claimsPrincipal)
            .Returns(userId);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyOrganizationDetailsByUserAsync(userId, ProviderUserStatusType.Confirmed)
            .Returns(providerUserOrganizations);

        await sutProvider.Sut.IsProviderUserForOrganization(claimsPrincipal, organizationId);
        await sutProvider.Sut.IsProviderUserForOrganization(claimsPrincipal, organizationId);

        await sutProvider.GetDependency<IProviderUserRepository>()
            .Received(1)
            .GetManyOrganizationDetailsByUserAsync(userId, ProviderUserStatusType.Confirmed);
    }
}
