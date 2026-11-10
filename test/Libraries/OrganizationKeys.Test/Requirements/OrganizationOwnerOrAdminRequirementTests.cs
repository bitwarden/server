using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.OrganizationKeys.Requirements;
using Xunit;

namespace Bit.OrganizationKeys.Test.Requirements;

public class OrganizationOwnerOrAdminRequirementTests
{
    [Theory]
    [InlineData(OrganizationUserType.Owner, true)]
    [InlineData(OrganizationUserType.Admin, true)]
    [InlineData(OrganizationUserType.User, false)]
    [InlineData(OrganizationUserType.Custom, false)]
    public async Task AuthorizeAsync_AuthorizesOwnersAndAdminsOnly(OrganizationUserType type, bool expected)
    {
        // Arrange - a Custom user with every permission is still not authorized
        var organizationClaims = new CurrentContextOrganization
        {
            Type = type,
            Permissions = new Permissions { ManageResetPassword = true, ManageUsers = true }
        };

        // Act
        var authorized = await new OrganizationOwnerOrAdminRequirement()
            .AuthorizeAsync(organizationClaims, () => Task.FromResult(false));

        // Assert
        Assert.Equal(expected, authorized);
    }

    [Fact]
    public async Task AuthorizeAsync_ProviderUserForTheOrganization_NotAuthorized()
    {
        // Arrange - a provider user who is not a member has no claims for the organization

        // Act
        var authorized = await new OrganizationOwnerOrAdminRequirement()
            .AuthorizeAsync(null, () => Task.FromResult(true));

        // Assert
        Assert.False(authorized);
    }
}
