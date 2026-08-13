using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v2;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
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
    public async Task ValidateAsync_WithValidUsers_ReturnsSuccess(
        SutProvider<RevokeOrganizationUsersValidator> sutProvider,
        Guid organizationId,
        Guid actingUserId,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser2)
    {
        // Arrange
        orgUser1.OrganizationId = orgUser2.OrganizationId = organizationId;
        orgUser1.UserId = Guid.NewGuid();
        orgUser2.UserId = Guid.NewGuid();

        UseRealValidationService(sutProvider);
        var actingUser = CreateActingUser(actingUserId, OrganizationUserType.Owner);
        var request = CreateValidationRequest(
            organizationId,
            [orgUser1, orgUser2],
            actingUser);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        // Act
        var results = (await sutProvider.Sut.ValidateAsync(request)).ToList();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.IsValid));
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WithRevokedUser_ReturnsErrorForThatUser(
        SutProvider<RevokeOrganizationUsersValidator> sutProvider,
        Guid organizationId,
        Guid actingUserId,
        [OrganizationUser(OrganizationUserStatusType.Revoked, OrganizationUserType.User)] OrganizationUser revokedUser)
    {
        // Arrange
        revokedUser.OrganizationId = organizationId;
        revokedUser.UserId = Guid.NewGuid();

        UseRealValidationService(sutProvider);
        var actingUser = CreateActingUser(actingUserId, OrganizationUserType.Owner);
        var request = CreateValidationRequest(
            organizationId,
            [revokedUser],
            actingUser);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        // Act
        var results = (await sutProvider.Sut.ValidateAsync(request)).ToList();

        // Assert
        Assert.Single(results);
        Assert.True(results.First().IsError);
        Assert.IsType<UserAlreadyRevoked>(results.First().AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenRevokingSelf_ReturnsErrorForThatUser(
        SutProvider<RevokeOrganizationUsersValidator> sutProvider,
        Guid organizationId,
        Guid actingUserId,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        // Arrange
        orgUser.OrganizationId = organizationId;
        orgUser.UserId = actingUserId;

        UseRealValidationService(sutProvider);
        var actingUser = CreateActingUser(actingUserId, OrganizationUserType.Owner);
        var request = CreateValidationRequest(
            organizationId,
            [orgUser],
            actingUser);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        // Act
        var results = (await sutProvider.Sut.ValidateAsync(request)).ToList();

        // Assert
        Assert.Single(results);
        Assert.True(results.First().IsError);
        Assert.IsType<CannotRevokeYourself>(results.First().AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenNonOwnerRevokesOwner_ReturnsErrorForThatUser(
        SutProvider<RevokeOrganizationUsersValidator> sutProvider,
        Guid organizationId,
        Guid actingUserId,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser ownerUser)
    {
        // Arrange
        ownerUser.OrganizationId = organizationId;
        ownerUser.UserId = Guid.NewGuid();

        UseRealValidationService(sutProvider);
        var actingUser = CreateActingUser(actingUserId, OrganizationUserType.Admin);
        var request = CreateValidationRequest(
            organizationId,
            [ownerUser],
            actingUser);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        // Act
        var results = (await sutProvider.Sut.ValidateAsync(request)).ToList();

        // Assert
        Assert.Single(results);
        Assert.True(results.First().IsError);
        Assert.IsType<OnlyOwnersCanManageOwners>(results.First().AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenOwnerRevokesOwner_ReturnsSuccess(
        SutProvider<RevokeOrganizationUsersValidator> sutProvider,
        Guid organizationId,
        Guid actingUserId,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser ownerUser)
    {
        // Arrange
        ownerUser.OrganizationId = organizationId;
        ownerUser.UserId = Guid.NewGuid();

        UseRealValidationService(sutProvider);
        var actingUser = CreateActingUser(actingUserId, OrganizationUserType.Owner);
        var request = CreateValidationRequest(
            organizationId,
            [ownerUser],
            actingUser);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        // Act
        var results = (await sutProvider.Sut.ValidateAsync(request)).ToList();

        // Assert
        Assert.Single(results);
        Assert.True(results.First().IsValid);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WithMultipleUsers_SomeValid_ReturnsMixedResults(
        SutProvider<RevokeOrganizationUsersValidator> sutProvider,
        Guid organizationId,
        Guid actingUserId,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser validUser,
        [OrganizationUser(OrganizationUserStatusType.Revoked, OrganizationUserType.User)] OrganizationUser revokedUser)
    {
        // Arrange
        validUser.OrganizationId = revokedUser.OrganizationId = organizationId;
        validUser.UserId = Guid.NewGuid();
        revokedUser.UserId = Guid.NewGuid();

        UseRealValidationService(sutProvider);
        var actingUser = CreateActingUser(actingUserId, OrganizationUserType.Owner);
        var request = CreateValidationRequest(
            organizationId,
            [validUser, revokedUser],
            actingUser);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        // Act
        var results = (await sutProvider.Sut.ValidateAsync(request)).ToList();

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
    public async Task ValidateAsync_WithSystemUser_DoesNotRequireActingUserId(
        SutProvider<RevokeOrganizationUsersValidator> sutProvider,
        Guid organizationId,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        // Arrange
        orgUser.OrganizationId = organizationId;
        orgUser.UserId = Guid.NewGuid();

        var actingUser = CreateActingUser(null, systemUserType: EventSystemUser.SCIM);
        var request = CreateValidationRequest(
            organizationId,
            [orgUser],
            actingUser);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        // Act
        var results = (await sutProvider.Sut.ValidateAsync(request)).ToList();

        // Assert
        Assert.Single(results);
        Assert.True(results.First().IsValid);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WithSystemUser_RevokingOwner_ReturnsSuccess(
        SutProvider<RevokeOrganizationUsersValidator> sutProvider,
        Guid organizationId,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser ownerUser)
    {
        // Arrange
        ownerUser.OrganizationId = organizationId;
        ownerUser.UserId = Guid.NewGuid();

        var actingUser = CreateActingUser(null, systemUserType: EventSystemUser.SCIM);
        var request = CreateValidationRequest(
            organizationId,
            [ownerUser],
            actingUser);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        // Act
        var results = (await sutProvider.Sut.ValidateAsync(request)).ToList();

        // Assert
        Assert.Single(results);
        Assert.True(results.First().IsValid);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenRevokingLastOwner_ReturnsErrorForThatUser(
        SutProvider<RevokeOrganizationUsersValidator> sutProvider,
        Guid organizationId,
        Guid actingUserId,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser lastOwner)
    {
        // Arrange
        lastOwner.OrganizationId = organizationId;
        lastOwner.UserId = Guid.NewGuid();

        UseRealValidationService(sutProvider);
        var actingUser = CreateActingUser(actingUserId, OrganizationUserType.Owner); // Is an owner
        var request = CreateValidationRequest(
            organizationId,
            [lastOwner],
            actingUser);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(false);

        // Act
        var results = (await sutProvider.Sut.ValidateAsync(request)).ToList();

        // Assert
        Assert.Single(results);
        Assert.True(results.First().IsError);
        Assert.IsType<MustHaveConfirmedOwner>(results.First().AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WithMultipleValidationErrors_ReturnsAllErrors(
        SutProvider<RevokeOrganizationUsersValidator> sutProvider,
        Guid organizationId,
        Guid actingUserId,
        [OrganizationUser(OrganizationUserStatusType.Revoked, OrganizationUserType.User)] OrganizationUser revokedUser,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser ownerUser)
    {
        // Arrange
        revokedUser.OrganizationId = ownerUser.OrganizationId = organizationId;
        revokedUser.UserId = Guid.NewGuid();
        ownerUser.UserId = Guid.NewGuid();

        UseRealValidationService(sutProvider);
        var actingUser = CreateActingUser(actingUserId, OrganizationUserType.Admin); // Not an owner
        var request = CreateValidationRequest(
            organizationId,
            [revokedUser, ownerUser],
            actingUser);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        // Act
        var results = (await sutProvider.Sut.ValidateAsync(request)).ToList();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.IsError));

        Assert.Contains(results, r => r.AsError is UserAlreadyRevoked);
        Assert.Contains(results, r => r.AsError is OnlyOwnersCanManageOwners);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenCustomUserRevokesAdmin_ReturnsErrorForThatUser(
        SutProvider<RevokeOrganizationUsersValidator> sutProvider,
        Guid organizationId,
        Guid actingUserId,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Admin)] OrganizationUser adminUser)
    {
        // Arrange
        adminUser.OrganizationId = organizationId;
        adminUser.UserId = Guid.NewGuid();

        UseRealValidationService(sutProvider);
        var request = CreateValidationRequest(
            organizationId,
            [adminUser],
            CreateActingUser(actingUserId, OrganizationUserType.Custom, new Permissions { ManageUsers = true }));

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        // Act
        var results = (await sutProvider.Sut.ValidateAsync(request)).ToList();

        // Assert
        Assert.Single(results);
        Assert.True(results.First().IsError);
        Assert.IsType<CustomUsersCannotManageAdminsOrOwners>(results.First().AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenAdminRevokesAdmin_ReturnsSuccess(
        SutProvider<RevokeOrganizationUsersValidator> sutProvider,
        Guid organizationId,
        Guid actingUserId,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Admin)] OrganizationUser adminUser)
    {
        // Arrange
        adminUser.OrganizationId = organizationId;
        adminUser.UserId = Guid.NewGuid();

        UseRealValidationService(sutProvider);
        var request = CreateValidationRequest(
            organizationId,
            [adminUser],
            CreateActingUser(actingUserId, OrganizationUserType.Admin));

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        // Act
        var results = (await sutProvider.Sut.ValidateAsync(request)).ToList();

        // Assert
        Assert.Single(results);
        Assert.True(results.First().IsValid);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenSystemUserRevokesAdmin_ReturnsSuccess(
        SutProvider<RevokeOrganizationUsersValidator> sutProvider,
        Guid organizationId,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Admin)] OrganizationUser adminUser)
    {
        // Arrange
        adminUser.OrganizationId = organizationId;
        adminUser.UserId = Guid.NewGuid();

        var actingUser = CreateActingUser(null, systemUserType: EventSystemUser.SCIM);
        var request = CreateValidationRequest(
            organizationId,
            [adminUser],
            actingUser);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        // Act
        var results = (await sutProvider.Sut.ValidateAsync(request)).ToList();

        // Assert
        Assert.Single(results);
        Assert.True(results.First().IsValid);
    }

    /// <summary>
    /// Replaces the auto-mocked <see cref="IOrganizationUserValidationService"/> with a real instance (backed by a
    /// provider-repository stub reporting no provider memberships), so these tests exercise the real "can manage"
    /// hierarchy instead of re-implementing it as a mock.
    /// </summary>
    private static void UseRealValidationService(SutProvider<RevokeOrganizationUsersValidator> sutProvider)
    {
        var providerUserRepository = Substitute.For<IProviderUserRepository>();
        providerUserRepository
            .GetManyOrganizationDetailsByUserAsync(Arg.Any<Guid>(), ProviderUserStatusType.Confirmed)
            .Returns([]);

        var validationService = new OrganizationUserValidationService(
            providerUserRepository, Substitute.For<IOrganizationUserRepository>());

        // Must match the constructor parameter name: the auto-created mock is already registered under this name,
        // so overriding under a different (e.g. default empty) name would be shadowed by it.
        sutProvider.SetDependency<IOrganizationUserValidationService>(validationService, "organizationUserValidationService");
        sutProvider.Create();
    }

    private static IActingUser CreateActingUser(Guid? userId, OrganizationUserType? type = null,
        Permissions? permissions = null, EventSystemUser? systemUserType = null) =>
        (userId, systemUserType) switch
        {
            ({ } id, _) => new StandardUser(id, type is OrganizationUserType.Owner, type, permissions),
            (null, { } sysType) => new SystemUser(sysType)
        };

    private static RevokeOrganizationUsersValidationRequest CreateValidationRequest(
        Guid organizationId,
        ICollection<OrganizationUser> organizationUsers,
        IActingUser actingUser)
    {
        return new RevokeOrganizationUsersValidationRequest(
            organizationId,
            organizationUsers,
            actingUser,
            RevocationReason.Manual
        );
    }
}
