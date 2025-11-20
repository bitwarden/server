using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v2;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v2;

[SutProviderCustomize]
public class RevokeOrganizationUsersValidatorTests
{
    [Theory]
    [BitAutoData]
    public void Validate_WithValidUsers_ReturnsSuccess(
        SutProvider<RevokeOrganizationUsersValidator> sutProvider,
        Guid organizationId,
        Guid actingUserId,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser2)
    {
        // Arrange
        orgUser1.OrganizationId = orgUser2.OrganizationId = organizationId;
        orgUser1.UserId = Guid.NewGuid();
        orgUser2.UserId = Guid.NewGuid();

        var actingUser = CreateActingUser(actingUserId, false, null);
        var request = CreateValidationRequest(
            organizationId,
            [orgUser1, orgUser2],
            actingUser,
            organization);

        // Act
        var results = sutProvider.Sut.Validate(request).ToList();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.IsValid));
    }

    [Theory]
    [BitAutoData]
    public void Validate_WithRevokedUser_ReturnsErrorForThatUser(
        SutProvider<RevokeOrganizationUsersValidator> sutProvider,
        Guid organizationId,
        Guid actingUserId,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Revoked, OrganizationUserType.User)] OrganizationUser revokedUser)
    {
        // Arrange
        revokedUser.OrganizationId = organizationId;
        revokedUser.UserId = Guid.NewGuid();

        var actingUser = CreateActingUser(actingUserId, false, null);
        var request = CreateValidationRequest(
            organizationId,
            [revokedUser],
            actingUser,
            organization);

        // Act
        var results = sutProvider.Sut.Validate(request).ToList();

        // Assert
        Assert.Single(results);
        Assert.True(results.First().IsError);
        Assert.IsType<UserAlreadyRevoked>(results.First().AsError);
    }

    [Theory]
    [BitAutoData]
    public void Validate_WhenRevokingSelf_ReturnsErrorForThatUser(
        SutProvider<RevokeOrganizationUsersValidator> sutProvider,
        Guid organizationId,
        Guid actingUserId,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        // Arrange
        orgUser.OrganizationId = organizationId;
        orgUser.UserId = actingUserId; // Same as acting user

        var actingUser = CreateActingUser(actingUserId, false, null);
        var request = CreateValidationRequest(
            organizationId,
            [orgUser],
            actingUser,
            organization);

        // Act
        var results = sutProvider.Sut.Validate(request).ToList();

        // Assert
        Assert.Single(results);
        Assert.True(results.First().IsError);
        Assert.IsType<CannotRevokeYourself>(results.First().AsError);
    }

    [Theory]
    [BitAutoData]
    public void Validate_WhenNonOwnerRevokesOwner_ReturnsErrorForThatUser(
        SutProvider<RevokeOrganizationUsersValidator> sutProvider,
        Guid organizationId,
        Guid actingUserId,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser ownerUser)
    {
        // Arrange
        ownerUser.OrganizationId = organizationId;
        ownerUser.UserId = Guid.NewGuid();

        var actingUser = CreateActingUser(actingUserId, false, null); // Not an owner
        var request = CreateValidationRequest(
            organizationId,
            [ownerUser],
            actingUser,
            organization);

        // Act
        var results = sutProvider.Sut.Validate(request).ToList();

        // Assert
        Assert.Single(results);
        Assert.True(results.First().IsError);
        Assert.IsType<OnlyOwnersCanRevokeOwners>(results.First().AsError);
    }

    [Theory]
    [BitAutoData]
    public void Validate_WhenOwnerRevokesOwner_ReturnsSuccess(
        SutProvider<RevokeOrganizationUsersValidator> sutProvider,
        Guid organizationId,
        Guid actingUserId,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser ownerUser)
    {
        // Arrange
        ownerUser.OrganizationId = organizationId;
        ownerUser.UserId = Guid.NewGuid();

        var actingUser = CreateActingUser(actingUserId, true, null); // Is an owner
        var request = CreateValidationRequest(
            organizationId,
            [ownerUser],
            actingUser,
            organization);

        // Act
        var results = sutProvider.Sut.Validate(request).ToList();

        // Assert
        Assert.Single(results);
        Assert.True(results.First().IsValid);
    }

    [Theory]
    [BitAutoData]
    public void Validate_WithMultipleUsers_SomeValid_ReturnsMixedResults(
        SutProvider<RevokeOrganizationUsersValidator> sutProvider,
        Guid organizationId,
        Guid actingUserId,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser validUser,
        [OrganizationUser(OrganizationUserStatusType.Revoked, OrganizationUserType.User)] OrganizationUser revokedUser)
    {
        // Arrange
        validUser.OrganizationId = revokedUser.OrganizationId = organizationId;
        validUser.UserId = Guid.NewGuid();
        revokedUser.UserId = Guid.NewGuid();

        var actingUser = CreateActingUser(actingUserId, false, null);
        var request = CreateValidationRequest(
            organizationId,
            [validUser, revokedUser],
            actingUser,
            organization);

        // Act
        var results = sutProvider.Sut.Validate(request).ToList();

        // Assert
        Assert.Equal(2, results.Count);

        var validResult = results.Single(r => r.Request.Id == validUser.Id);
        var errorResult = results.Single(r => r.Request.Id == revokedUser.Id);

        Assert.True(validResult.IsValid);
        Assert.True(errorResult.IsError);
        Assert.IsType<UserAlreadyRevoked>(errorResult.AsError);
    }

    [Theory]
    [BitAutoData]
    public void Validate_WithSystemUser_DoesNotRequireActingUserId(
        SutProvider<RevokeOrganizationUsersValidator> sutProvider,
        Guid organizationId,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        // Arrange
        orgUser.OrganizationId = organizationId;
        orgUser.UserId = Guid.NewGuid();

        var actingUser = CreateActingUser(null, false, EventSystemUser.SCIM);
        var request = CreateValidationRequest(
            organizationId,
            [orgUser],
            actingUser,
            organization);

        // Act
        var results = sutProvider.Sut.Validate(request).ToList();

        // Assert
        Assert.Single(results);
        Assert.True(results.First().IsValid);
    }

    [Theory]
    [BitAutoData]
    public void Validate_WithMultipleValidationErrors_ReturnsAllErrors(
        SutProvider<RevokeOrganizationUsersValidator> sutProvider,
        Guid organizationId,
        Guid actingUserId,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Revoked, OrganizationUserType.User)] OrganizationUser revokedUser,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser ownerUser)
    {
        // Arrange
        revokedUser.OrganizationId = ownerUser.OrganizationId = organizationId;
        revokedUser.UserId = Guid.NewGuid();
        ownerUser.UserId = Guid.NewGuid();

        var actingUser = CreateActingUser(actingUserId, false, null); // Not an owner
        var request = CreateValidationRequest(
            organizationId,
            [revokedUser, ownerUser],
            actingUser,
            organization);

        // Act
        var results = sutProvider.Sut.Validate(request).ToList();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.IsError));

        Assert.Contains(results, r => r.AsError is UserAlreadyRevoked);
        Assert.Contains(results, r => r.AsError is OnlyOwnersCanRevokeOwners);
    }

    private static IActingUser CreateActingUser(Guid? userId, bool isOwnerOrProvider, EventSystemUser? systemUserType)
    {
        var actingUser = Substitute.For<IActingUser>();
        actingUser.UserId.Returns(userId);
        actingUser.IsOrganizationOwnerOrProvider.Returns(isOwnerOrProvider);
        actingUser.SystemUserType.Returns(systemUserType);
        return actingUser;
    }

    private static RevokeOrganizationUsersValidationRequest CreateValidationRequest(
        Guid organizationId,
        ICollection<OrganizationUser> organizationUsers,
        IActingUser actingUser,
        Organization organization)
    {
        return new RevokeOrganizationUsersValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserIdsToRevoke = organizationUsers.Select(u => u.Id).ToList(),
            PerformedBy = actingUser,
            OrganizationUsersToRevoke = organizationUsers,
            Organization = organization
        };
    }
}
