using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Sso;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.Sso;

[SutProviderCustomize]
public class UserSsoOrganizationIdentifierQueryTests
{
    [Theory, BitAutoData]
    public async Task GetSsoOrganizationIdentifierAsync_UserHasSingleConfirmedOrganization_ReturnsIdentifier(
        SutProvider<UserSsoOrganizationIdentifierQuery> sutProvider,
        Guid userId,
        Organization organization,
        OrganizationUser organizationUser)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.OrganizationId = organization.Id;
        organizationUser.Status = OrganizationUserStatusType.Confirmed;
        organization.Identifier = "test-org-identifier";

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns([organizationUser]);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        // Act
        var result = await sutProvider.Sut.GetSsoOrganizationIdentifierAsync(userId);

        // Assert
        Assert.Equal("test-org-identifier", result);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .GetManyByUserAsync(userId);
        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .GetByIdAsync(organization.Id);
    }

    [Theory, BitAutoData]
    public async Task GetSsoOrganizationIdentifierAsync_UserHasNoOrganizations_ReturnsNull(
        SutProvider<UserSsoOrganizationIdentifierQuery> sutProvider,
        Guid userId)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns(Array.Empty<OrganizationUser>());

        // Act
        var result = await sutProvider.Sut.GetSsoOrganizationIdentifierAsync(userId);

        // Assert
        Assert.Null(result);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .GetManyByUserAsync(userId);
        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceive()
            .GetByIdAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task GetSsoOrganizationIdentifierAsync_UserHasMultipleConfirmedOrganizations_ReturnsNull(
        SutProvider<UserSsoOrganizationIdentifierQuery> sutProvider,
        Guid userId,
        OrganizationUser organizationUser1,
        OrganizationUser organizationUser2)
    {
        // Arrange
        organizationUser1.UserId = userId;
        organizationUser1.Status = OrganizationUserStatusType.Confirmed;
        organizationUser2.UserId = userId;
        organizationUser2.Status = OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns([organizationUser1, organizationUser2]);

        // Act
        var result = await sutProvider.Sut.GetSsoOrganizationIdentifierAsync(userId);

        // Assert
        Assert.Null(result);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .GetManyByUserAsync(userId);
        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceive()
            .GetByIdAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Invited)]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Revoked)]
    public async Task GetSsoOrganizationIdentifierAsync_UserHasOnlyInvitedOrganization_ReturnsNull(
        OrganizationUserStatusType status,
        SutProvider<UserSsoOrganizationIdentifierQuery> sutProvider,
        Guid userId,
        OrganizationUser organizationUser)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.Status = status;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns([organizationUser]);

        // Act
        var result = await sutProvider.Sut.GetSsoOrganizationIdentifierAsync(userId);

        // Assert
        Assert.Null(result);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .GetManyByUserAsync(userId);
        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceive()
            .GetByIdAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task GetSsoOrganizationIdentifierAsync_UserHasMixedStatusOrganizations_OnlyOneConfirmed_ReturnsIdentifier(
        SutProvider<UserSsoOrganizationIdentifierQuery> sutProvider,
        Guid userId,
        Organization organization,
        OrganizationUser confirmedOrgUser,
        OrganizationUser invitedOrgUser,
        OrganizationUser revokedOrgUser)
    {
        // Arrange
        confirmedOrgUser.UserId = userId;
        confirmedOrgUser.OrganizationId = organization.Id;
        confirmedOrgUser.Status = OrganizationUserStatusType.Confirmed;

        invitedOrgUser.UserId = userId;
        invitedOrgUser.Status = OrganizationUserStatusType.Invited;

        revokedOrgUser.UserId = userId;
        revokedOrgUser.Status = OrganizationUserStatusType.Revoked;

        organization.Identifier = "mixed-status-org";

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns(new[] { confirmedOrgUser, invitedOrgUser, revokedOrgUser });

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        // Act
        var result = await sutProvider.Sut.GetSsoOrganizationIdentifierAsync(userId);

        // Assert
        Assert.Equal("mixed-status-org", result);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .GetManyByUserAsync(userId);
        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .GetByIdAsync(organization.Id);
    }

    [Theory, BitAutoData]
    public async Task GetSsoOrganizationIdentifierAsync_OrganizationNotFound_ReturnsNull(
        SutProvider<UserSsoOrganizationIdentifierQuery> sutProvider,
        Guid userId,
        OrganizationUser organizationUser)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.Status = OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns([organizationUser]);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationUser.OrganizationId)
            .Returns((Organization)null);

        // Act
        var result = await sutProvider.Sut.GetSsoOrganizationIdentifierAsync(userId);

        // Assert
        Assert.Null(result);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .GetManyByUserAsync(userId);
        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .GetByIdAsync(organizationUser.OrganizationId);
    }

    [Theory, BitAutoData]
    public async Task GetSsoOrganizationIdentifierAsync_OrganizationIdentifierIsNull_ReturnsNull(
        SutProvider<UserSsoOrganizationIdentifierQuery> sutProvider,
        Guid userId,
        Organization organization,
        OrganizationUser organizationUser)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.OrganizationId = organization.Id;
        organizationUser.Status = OrganizationUserStatusType.Confirmed;
        organization.Identifier = null;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns(new[] { organizationUser });

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        // Act
        var result = await sutProvider.Sut.GetSsoOrganizationIdentifierAsync(userId);

        // Assert
        Assert.Null(result);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .GetManyByUserAsync(userId);
        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .GetByIdAsync(organization.Id);
    }

    [Theory, BitAutoData]
    public async Task GetSsoOrganizationIdentifierAsync_OrganizationIdentifierIsEmpty_ReturnsEmpty(
        SutProvider<UserSsoOrganizationIdentifierQuery> sutProvider,
        Guid userId,
        Organization organization,
        OrganizationUser organizationUser)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.OrganizationId = organization.Id;
        organizationUser.Status = OrganizationUserStatusType.Confirmed;
        organization.Identifier = string.Empty;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns(new[] { organizationUser });

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        // Act
        var result = await sutProvider.Sut.GetSsoOrganizationIdentifierAsync(userId);

        // Assert
        Assert.Equal(string.Empty, result);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .GetManyByUserAsync(userId);
        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .GetByIdAsync(organization.Id);
    }
}
