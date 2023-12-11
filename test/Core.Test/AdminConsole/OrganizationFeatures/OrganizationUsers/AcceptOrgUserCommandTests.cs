using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.OrganizationFeatures.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Fakes;
using Microsoft.AspNetCore.DataProtection;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationUsers;

// Note: test names follow MethodName_StateUnderTest_ExpectedBehavior pattern.
[SutProviderCustomize]
public class AcceptOrgUserCommandTests
{
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IOrgUserInviteTokenableFactory _orgUserInviteTokenableFactory = Substitute.For<IOrgUserInviteTokenableFactory>();
    private readonly IDataProtectorTokenFactory<OrgUserInviteTokenable> _orgUserInviteTokenDataFactory = new FakeDataProtectorTokenFactory<OrgUserInviteTokenable>();

    // Base AcceptOrgUserAsync method tests ----------------------------------------------------------------------------

    [Theory]
    [BitAutoData]
    public async Task AcceptOrgUser_InvitedUserToSingleOrg_AcceptsOrgUser(
        SutProvider<AcceptOrgUserCommand> sutProvider,
        User user, Organization org, OrganizationUser orgUser, OrganizationUserUserDetails adminUserDetails)
    {
        // Arrange
        SetupCommonAcceptOrgUserMocks(sutProvider, user, org, orgUser, adminUserDetails);

        // Act
        var resultOrgUser = await sutProvider.Sut.AcceptOrgUserAsync(orgUser, user, _userService);

        // Assert
        // Verify returned org user details
        AssertValidAcceptedOrgUser(resultOrgUser, orgUser, user);

        // Verify org repository called with updated orgUser
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).ReplaceAsync(
            Arg.Is<OrganizationUser>(ou => ou.Id == orgUser.Id && ou.Status == OrganizationUserStatusType.Accepted));

        // Verify emails sent to admin
        await sutProvider.GetDependency<IMailService>().Received(1).SendOrganizationAcceptedEmailAsync(
            Arg.Is<Organization>(o => o.Id == org.Id),
            Arg.Is<string>(e => e == user.Email),
            Arg.Is<IEnumerable<string>>(a => a.Contains(adminUserDetails.Email))
        );
    }

    [Theory]
    [BitAutoData]
    public async Task AcceptOrgUser_OrgUserStatusIsRevoked_ReturnsBadRequest(
        SutProvider<AcceptOrgUserCommand> sutProvider,
        User user, Organization org, OrganizationUser orgUser, OrganizationUserUserDetails adminUserDetails)
    {
        // Common setup
        SetupCommonAcceptOrgUserMocks(sutProvider, user, org, orgUser, adminUserDetails);

        // Revoke user status
        orgUser.Status = OrganizationUserStatusType.Revoked;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AcceptOrgUserAsync(orgUser, user, _userService));

        Assert.Equal("Your organization access has been revoked.", exception.Message);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    public async Task AcceptOrgUser_OrgUserStatusIsNotInvited_ThrowsBadRequest(
        OrganizationUserStatusType orgUserStatus,
        SutProvider<AcceptOrgUserCommand> sutProvider,
        User user, Organization org, OrganizationUser orgUser, OrganizationUserUserDetails adminUserDetails)
    {
        // Arrange
        SetupCommonAcceptOrgUserMocks(sutProvider, user, org, orgUser, adminUserDetails);

        // Set status to something other than invited
        orgUser.Status = orgUserStatus;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AcceptOrgUserAsync(orgUser, user, _userService));

        Assert.Equal("Already accepted.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AcceptOrgUser_UserJoiningOrgWithSingleOrgPolicyWhileInAnotherOrg_ThrowsBadRequest(
        SutProvider<AcceptOrgUserCommand> sutProvider,
        User user, Organization org, OrganizationUser orgUser, OrganizationUserUserDetails adminUserDetails)
    {
        // Arrange
        SetupCommonAcceptOrgUserMocks(sutProvider, user, org, orgUser, adminUserDetails);

        //  Make user part of another org
        var otherOrgUser = new OrganizationUser { UserId = user.Id, OrganizationId = Guid.NewGuid() }; // random org ID
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(user.Id)
            .Returns(Task.FromResult<ICollection<OrganizationUser>>(new List<OrganizationUser> { otherOrgUser }));

        // Make organization they are trying to join have the single org policy
        var singleOrgPolicy = new OrganizationUserPolicyDetails { OrganizationId = orgUser.OrganizationId };
        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(user.Id, PolicyType.SingleOrg, OrganizationUserStatusType.Invited)
            .Returns(Task.FromResult<ICollection<OrganizationUserPolicyDetails>>(
                new List<OrganizationUserPolicyDetails> { singleOrgPolicy }));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AcceptOrgUserAsync(orgUser, user, _userService));

        Assert.Equal("You may not join this organization until you leave or remove all other organizations.",
            exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AcceptOrgUserAsync_UserInOrgWithSingleOrgPolicyAlready_ThrowsBadRequest(
        SutProvider<AcceptOrgUserCommand> sutProvider,
        User user, Organization org, OrganizationUser orgUser, OrganizationUserUserDetails adminUserDetails)
    {
        // Arrange
        SetupCommonAcceptOrgUserMocks(sutProvider, user, org, orgUser, adminUserDetails);

        // Mock that user is part of an org that has the single org policy
        sutProvider.GetDependency<IPolicyService>()
            .AnyPoliciesApplicableToUserAsync(user.Id, PolicyType.SingleOrg)
            .Returns(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AcceptOrgUserAsync(orgUser, user, _userService));

        Assert.Equal(
            "You cannot join this organization because you are a member of another organization which forbids it",
            exception.Message);
    }


    [Theory]
    [BitAutoData]
    public async Task AcceptOrgUserAsync_UserWithout2FAJoining2FARequiredOrg_ThrowsBadRequest(
        SutProvider<AcceptOrgUserCommand> sutProvider,
        User user, Organization org, OrganizationUser orgUser, OrganizationUserUserDetails adminUserDetails)
    {
        // Arrange
        SetupCommonAcceptOrgUserMocks(sutProvider, user, org, orgUser, adminUserDetails);

        // User doesn't have 2FA enabled
        _userService.TwoFactorIsEnabledAsync(user).Returns(false);

        // Organization they are trying to join requires 2FA
        var twoFactorPolicy = new OrganizationUserPolicyDetails { OrganizationId = orgUser.OrganizationId };
        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(user.Id, PolicyType.TwoFactorAuthentication,
                OrganizationUserStatusType.Invited)
            .Returns(Task.FromResult<ICollection<OrganizationUserPolicyDetails>>(
                new List<OrganizationUserPolicyDetails> { twoFactorPolicy }));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AcceptOrgUserAsync(orgUser, user, _userService));

        Assert.Equal("You cannot join this organization until you enable two-step login on your user account.",
            exception.Message);
    }


    // AcceptOrgUserByOrgIdAsync tests --------------------------------------------------------------------------------

    [Theory]
    [EphemeralDataProtectionAutoData]
    public async Task AcceptOrgUserByToken_OldToken_AcceptsUserAndVerifiesEmail(
        SutProvider<AcceptOrgUserCommand> sutProvider,
        User user, Organization org, OrganizationUser orgUser, OrganizationUserUserDetails adminUserDetails)
    {
        // Arrange
        SetupCommonAcceptOrgUserMocks(sutProvider, user, org, orgUser, adminUserDetails);
        SetupCommonAcceptOrgUserByTokenMocks(sutProvider, user, orgUser);

        var oldToken = CreateOldToken(sutProvider, orgUser);

        // Act
        var resultOrgUser = await sutProvider.Sut.AcceptOrgUserByEmailTokenAsync(orgUser.Id, user, oldToken, _userService);

        // Assert
        AssertValidAcceptedOrgUser(resultOrgUser, orgUser, user);

        // Verify user email verified logic
        Assert.True(user.EmailVerified);
        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(
            Arg.Is<User>(u => u.Id == user.Id && u.Email == user.Email && user.EmailVerified == true));
    }

    [Theory]
    [BitAutoData]
    public async Task AcceptOrgUserByToken_NewToken_AcceptsUserAndVerifiesEmail(
        SutProvider<AcceptOrgUserCommand> sutProvider,
        User user, Organization org, OrganizationUser orgUser, OrganizationUserUserDetails adminUserDetails)
    {
        // Arrange
        // Setup FakeDataProtectorTokenFactory for creating new tokens - this must come first in order
        // to avoid resetting mocks
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();

        SetupCommonAcceptOrgUserMocks(sutProvider, user, org, orgUser, adminUserDetails);
        SetupCommonAcceptOrgUserByTokenMocks(sutProvider, user, orgUser);

        // Must come after common mocks as they mutate the org user.
        // Mock tokenable factory to return a token that expires in 5 days
        _orgUserInviteTokenableFactory.CreateToken(orgUser).Returns(new OrgUserInviteTokenable(orgUser)
        {
            ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(5))
        });

        var newToken = CreateNewToken(orgUser);

        // Act
        var resultOrgUser = await sutProvider.Sut.AcceptOrgUserByEmailTokenAsync(orgUser.Id, user, newToken, _userService);

        // Assert
        AssertValidAcceptedOrgUser(resultOrgUser, orgUser, user);

        // Verify user email verified logic
        Assert.True(user.EmailVerified);
        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(
            Arg.Is<User>(u => u.Id == user.Id && u.Email == user.Email && user.EmailVerified == true));
    }

    [Theory]
    [BitAutoData]
    public async Task AcceptOrgUserByToken_NullOrgUser_ThrowsBadRequest(
        SutProvider<AcceptOrgUserCommand> sutProvider,
        User user, Guid orgUserId)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(orgUserId).Returns((OrganizationUser)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AcceptOrgUserByEmailTokenAsync(orgUserId, user, "token", _userService));

        Assert.Equal("User invalid.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AcceptOrgUserByToken_GenericInvalidToken_ThrowsBadRequest(
        SutProvider<AcceptOrgUserCommand> sutProvider,
        User user, OrganizationUser orgUser)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(orgUser.Id)
            .Returns(Task.FromResult(orgUser));

        var invalidToken = "invalidToken";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AcceptOrgUserByEmailTokenAsync(orgUser.Id, user, invalidToken, _userService));

        Assert.Equal("Invalid token.", exception.Message);
    }

    [Theory]
    [EphemeralDataProtectionAutoData]
    public async Task AcceptOrgUserByToken_ExpiredOldToken_ThrowsBadRequest(
        SutProvider<AcceptOrgUserCommand> sutProvider,
        User user, Organization org, OrganizationUser orgUser, OrganizationUserUserDetails adminUserDetails)
    {
        // Arrange
        SetupCommonAcceptOrgUserMocks(sutProvider, user, org, orgUser, adminUserDetails);
        SetupCommonAcceptOrgUserByTokenMocks(sutProvider, user, orgUser);

        // As the old token simply set a timestamp which was later compared against the
        // OrganizationInviteExpirationHours global setting to determine if it was expired or not,
        // we can simply set the expiration to 24 hours ago to simulate an expired token.
        sutProvider.GetDependency<IGlobalSettings>().OrganizationInviteExpirationHours.Returns(-24);

        var oldToken = CreateOldToken(sutProvider, orgUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AcceptOrgUserByEmailTokenAsync(orgUser.Id, user, oldToken, _userService));

        Assert.Equal("Invalid token.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AcceptOrgUserByToken_ExpiredNewToken_ThrowsBadRequest(
        SutProvider<AcceptOrgUserCommand> sutProvider,
        User user, OrganizationUser orgUser)
    {
        // Arrange
        // Setup FakeDataProtectorTokenFactory for creating new tokens - this must come first in order
        // to avoid resetting mocks
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(orgUser.Id)
            .Returns(Task.FromResult(orgUser));

        // Must come after common mocks as they mutate the org user.
        // Mock tokenable factory to return a token that expired yesterday
        _orgUserInviteTokenableFactory.CreateToken(orgUser).Returns(new OrgUserInviteTokenable(orgUser)
        {
            ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(-1))
        });

        var newToken = CreateNewToken(orgUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AcceptOrgUserByEmailTokenAsync(orgUser.Id, user, newToken, _userService));

        Assert.Equal("Invalid token.", exception.Message);

    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Accepted,
        "Invitation already accepted. You will receive an email when your organization membership is confirmed.")]
    [BitAutoData(OrganizationUserStatusType.Confirmed,
        "You are already part of this organization.")]
    public async Task AcceptOrgUserByToken_UserAlreadyInOrg_ThrowsBadRequest(
        OrganizationUserStatusType statusType,
        string expectedErrorMessage,
        SutProvider<AcceptOrgUserCommand> sutProvider,
        User user, OrganizationUser orgUser)
    {
        // Arrange
        // Setup FakeDataProtectorTokenFactory for creating new tokens - this must come first in order
        // to avoid resetting mocks
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(orgUser.Id)
            .Returns(Task.FromResult(orgUser));

        // Indicate that a user with the given email already exists in the organization
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByOrganizationAsync(orgUser.OrganizationId, user.Email, true)
            .Returns(1);

        orgUser.Status = statusType;

        // Must come after common mocks as they mutate the org user.
        // Mock tokenable factory to return valid, new token that expires in 5 days
        _orgUserInviteTokenableFactory.CreateToken(orgUser).Returns(new OrgUserInviteTokenable(orgUser)
        {
            ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(5))
        });

        var newToken = CreateNewToken(orgUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AcceptOrgUserByEmailTokenAsync(orgUser.Id, user, newToken, _userService));

        Assert.Equal(expectedErrorMessage, exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AcceptOrgUserByToken_EmailMismatch_ThrowsBadRequest(
        SutProvider<AcceptOrgUserCommand> sutProvider,
        User user, OrganizationUser orgUser)
    {
        // Arrange
        // Setup FakeDataProtectorTokenFactory for creating new tokens - this must come first in order
        // to avoid resetting mocks
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();

        SetupCommonAcceptOrgUserByTokenMocks(sutProvider, user, orgUser);

        // Modify the orgUser's email to be different from the user's email to simulate the mismatch
        orgUser.Email = "mismatchedEmail@example.com";

        // Must come after common mocks as they mutate the org user.
        // Mock tokenable factory to return a token that expires in 5 days
        _orgUserInviteTokenableFactory.CreateToken(orgUser).Returns(new OrgUserInviteTokenable(orgUser)
        {
            ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(5))
        });

        var newToken = CreateNewToken(orgUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AcceptOrgUserByEmailTokenAsync(orgUser.Id, user, newToken, _userService));

        Assert.Equal("User email does not match invite.", exception.Message);
    }


    // AcceptOrgUserByOrgSsoIdAsync -----------------------------------------------------------------------------------

    [Theory]
    [BitAutoData]
    public async Task AcceptOrgUserByOrgSsoIdAsync_ValidData_AcceptsOrgUser(
        SutProvider<AcceptOrgUserCommand> sutProvider,
        User user, Organization org, OrganizationUser orgUser, OrganizationUserUserDetails adminUserDetails)
    {
        // Arrange
        SetupCommonAcceptOrgUserMocks(sutProvider, user, org, orgUser, adminUserDetails);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdentifierAsync(org.Identifier)
            .Returns(org);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(org.Id, user.Id)
            .Returns(orgUser);

        // Act
        var resultOrgUser = await sutProvider.Sut.AcceptOrgUserByOrgSsoIdAsync(org.Identifier, user, _userService);

        // Assert
        AssertValidAcceptedOrgUser(resultOrgUser, orgUser, user);
    }

    [Theory]
    [BitAutoData]
    public async Task AcceptOrgUserByOrgSsoIdAsync_InvalidOrg_ThrowsBadRequest(SutProvider<AcceptOrgUserCommand> sutProvider,
        string orgSsoIdentifier, User user)
    {
        // Arrange

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdentifierAsync(orgSsoIdentifier)
            .Returns((Organization)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AcceptOrgUserByOrgSsoIdAsync(orgSsoIdentifier, user, _userService));

        Assert.Equal("Organization invalid.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AcceptOrgUserByOrgSsoIdAsync_UserNotInOrg_ThrowsBadRequest(SutProvider<AcceptOrgUserCommand> sutProvider,
        Organization org, User user)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdentifierAsync(org.Identifier)
            .Returns(org);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(org.Id, user.Id)
            .Returns((OrganizationUser)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AcceptOrgUserByOrgSsoIdAsync(org.Identifier, user, _userService));

        Assert.Equal("User not found within organization.", exception.Message);
    }

    // AcceptOrgUserByOrgIdAsync ---------------------------------------------------------------------------------------

    [Theory]
    [BitAutoData]
    public async Task AcceptOrgUserByOrgId_ValidData_AcceptsOrgUser(
        SutProvider<AcceptOrgUserCommand> sutProvider,
        User user, Organization org, OrganizationUser orgUser, OrganizationUserUserDetails adminUserDetails)
    {
        // Arrange
        SetupCommonAcceptOrgUserMocks(sutProvider, user, org, orgUser, adminUserDetails);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(org.Id)
            .Returns(org);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(org.Id, user.Id)
            .Returns(orgUser);

        // Act
        var resultOrgUser = await sutProvider.Sut.AcceptOrgUserByOrgIdAsync(org.Id, user, _userService);

        // Assert
        AssertValidAcceptedOrgUser(resultOrgUser, orgUser, user);
    }

    [Theory]
    [BitAutoData]
    public async Task AcceptOrgUserByOrgId_InvalidOrg_ThrowsBadRequest(SutProvider<AcceptOrgUserCommand> sutProvider,
        Guid orgId, User user)
    {
        // Arrange

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(orgId)
            .Returns((Organization)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AcceptOrgUserByOrgIdAsync(orgId, user, _userService));

        Assert.Equal("Organization invalid.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AcceptOrgUserByOrgId_UserNotInOrg_ThrowsBadRequest(SutProvider<AcceptOrgUserCommand> sutProvider,
        Organization org, User user)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(org.Id)
            .Returns(org);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(org.Id, user.Id)
            .Returns((OrganizationUser)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AcceptOrgUserByOrgIdAsync(org.Id, user, _userService));

        Assert.Equal("User not found within organization.", exception.Message);
    }

    // Private helpers -------------------------------------------------------------------------------------------------

    /// <summary>
    ///  Asserts that the given org user is in the expected state after a successful AcceptOrgUserAsync call.
    ///  For use in happy path tests.
    /// </summary>
    private void AssertValidAcceptedOrgUser(OrganizationUser resultOrgUser, OrganizationUser expectedOrgUser, User user)
    {
        Assert.NotNull(resultOrgUser);
        Assert.Equal(OrganizationUserStatusType.Accepted, resultOrgUser.Status);
        Assert.Equal(expectedOrgUser, resultOrgUser);
        Assert.Equal(expectedOrgUser.Id, resultOrgUser.Id);
        Assert.Null(resultOrgUser.Email);
        Assert.Equal(user.Id, resultOrgUser.UserId);


    }

    private void SetupCommonAcceptOrgUserByTokenMocks(SutProvider<AcceptOrgUserCommand> sutProvider, User user, OrganizationUser orgUser)
    {
        sutProvider.GetDependency<IGlobalSettings>().OrganizationInviteExpirationHours.Returns(24);
        user.EmailVerified = false;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(orgUser.Id)
            .Returns(Task.FromResult(orgUser));

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByOrganizationAsync(orgUser.OrganizationId, user.Email, true)
            .Returns(0);
    }

    /// <summary>
    /// Sets up common mock behavior for the AcceptOrgUserAsync tests.
    /// This method initializes:
    /// - The invited user's email, status, type, and organization ID.
    /// - Ensures the user is not part of any other organizations.
    /// - Confirms the target organization doesn't have a single org policy.
    /// - Ensures the user doesn't belong to an organization with a single org policy.
    /// - Assumes the user doesn't have 2FA enabled and the organization doesn't require it.
    /// - Provides mock data for an admin to validate email functionality.
    /// - Returns the corresponding organization for the given org ID.
    /// </summary>
    private void SetupCommonAcceptOrgUserMocks(SutProvider<AcceptOrgUserCommand> sutProvider, User user,
        Organization org,
        OrganizationUser orgUser, OrganizationUserUserDetails adminUserDetails)
    {
        // Arrange
        orgUser.Email = user.Email;
        orgUser.Status = OrganizationUserStatusType.Invited;
        orgUser.Type = OrganizationUserType.User;
        orgUser.OrganizationId = org.Id;

        // User is not part of any other orgs
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(user.Id)
            .Returns(
                Task.FromResult<ICollection<OrganizationUser>>(new List<OrganizationUser>())
            );

        // Org they are trying to join does not have single org policy
        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(user.Id, PolicyType.SingleOrg, OrganizationUserStatusType.Invited)
            .Returns(
                Task.FromResult<ICollection<OrganizationUserPolicyDetails>>(
                    new List<OrganizationUserPolicyDetails>()
                )
            );

        // User is not part of any organization that applies the single org policy
        sutProvider.GetDependency<IPolicyService>()
            .AnyPoliciesApplicableToUserAsync(user.Id, PolicyType.SingleOrg)
            .Returns(false);

        // User doesn't have 2FA enabled
        _userService.TwoFactorIsEnabledAsync(user).Returns(false);

        // Org does not require 2FA
        sutProvider.GetDependency<IPolicyService>().GetPoliciesApplicableToUserAsync(user.Id,
                PolicyType.TwoFactorAuthentication, OrganizationUserStatusType.Invited)
            .Returns(Task.FromResult<ICollection<OrganizationUserPolicyDetails>>(
                new List<OrganizationUserPolicyDetails>()));

        // Provide at least 1 admin to test email functionality
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(orgUser.OrganizationId, OrganizationUserType.Admin)
            .Returns(Task.FromResult<IEnumerable<OrganizationUserUserDetails>>(
                new List<OrganizationUserUserDetails>() { adminUserDetails }
            ));

        // Return org
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(org.Id)
            .Returns(Task.FromResult(org));
    }


    private string CreateOldToken(SutProvider<AcceptOrgUserCommand> sutProvider,
        OrganizationUser organizationUser)
    {
        var dataProtector = sutProvider.GetDependency<IDataProtectionProvider>()
            .CreateProtector("OrganizationServiceDataProtector");

        // Token matching the format used in OrganizationService.InviteUserAsync
        var oldToken = dataProtector.Protect(
            $"OrganizationUserInvite {organizationUser.Id} {organizationUser.Email} {CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow)}");

        return oldToken;
    }

    private string CreateNewToken(OrganizationUser orgUser)
    {
        var orgUserInviteTokenable = _orgUserInviteTokenableFactory.CreateToken(orgUser);
        var protectedToken = _orgUserInviteTokenDataFactory.Protect(orgUserInviteTokenable);

        return protectedToken;
    }
}
