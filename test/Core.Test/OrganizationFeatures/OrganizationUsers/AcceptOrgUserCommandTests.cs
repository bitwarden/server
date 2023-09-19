using System.Text.Json;
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
using Microsoft.AspNetCore.DataProtection;
using NSubstitute;
using Xunit;
using Xunit.Sdk;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationUsers;


public class FakeDataProtectorTokenFactory<T> : IDataProtectorTokenFactory<T> where T : Tokenable, new()
{
    // Instead of real encryption, use a simple Dictionary to emulate protection/unprotection
    private readonly Dictionary<string, T> _tokenDatabase = new Dictionary<string, T>();

    public string Protect(T data)
    {
        // Generate a simple token representation
        var token = Guid.NewGuid().ToString();

        // Store the data against the token
        _tokenDatabase[token] = data;

        return token;
    }

    public T Unprotect(string token)
    {
        // If the token exists in the dictionary, return the corresponding data
        if (_tokenDatabase.TryGetValue(token, out var data))
        {
            return data;
        }

        // If the token doesn't exist, throw an exception similar to a decryption failure.
        throw new Exception("Failed to unprotect token.");
    }

    public bool TryUnprotect(string token, out T data)
    {
        try
        {
            data = Unprotect(token);
            return true;
        }
        catch
        {
            data = default;
            return false;
        }
    }

    public bool TokenValid(string token)
    {
        return _tokenDatabase.ContainsKey(token);
    }
}

// Note: test names follow MethodName_StateUnderTest_ExpectedBehavior pattern.
[SutProviderCustomize]
public class AcceptOrgUserCommandTests
{
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IOrgUserInviteTokenableFactory _orgUserInviteTokenableFactory = Substitute.For<IOrgUserInviteTokenableFactory>();

    // private readonly IDataProtectorTokenFactory<OrgUserInviteTokenable> _orgUserInviteTokenDataFactory = Substitute.For<IDataProtectorTokenFactory<OrgUserInviteTokenable>>();
    private readonly IDataProtectorTokenFactory<OrgUserInviteTokenable> _orgUserInviteTokenDataFactory = new FakeDataProtectorTokenFactory<OrgUserInviteTokenable>();

    [Theory]
    [BitAutoData]
    public async Task AcceptOrgUser_InvitedUserToSingleOrg_Success(
        SutProvider<AcceptOrgUserCommand> sutProvider,
        User user, Organization org, OrganizationUser orgUser, OrganizationUserUserDetails adminUserDetails)
    {
        // Arrange
        SetupCommonAcceptOrgUserMocks(sutProvider, user, org, orgUser, adminUserDetails);

        // Act
        var orgUserResult = await sutProvider.Sut.AcceptOrgUserAsync(orgUser, user, _userService);

        // Assert

        // Verify returned org user details
        Assert.NotNull(orgUserResult);
        Assert.Equal(OrganizationUserStatusType.Accepted, orgUserResult.Status);
        Assert.Equal(orgUser.Id, orgUserResult.Id);
        Assert.Null(orgUserResult.Email);

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
        var result = await sutProvider.Sut.AcceptOrgUserByTokenAsync(orgUser.Id, user, oldToken, _userService);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(OrganizationUserStatusType.Accepted, result.Status);
        Assert.Equal(orgUser.Id, result.Id);
        Assert.Null(result.Email);
        Assert.Equal(user.Id, result.UserId);

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
        SetupCommonAcceptOrgUserMocks(sutProvider, user, org, orgUser, adminUserDetails);
        SetupCommonAcceptOrgUserByTokenMocks(sutProvider, user, orgUser);

        // Mock tokenable factory to return a token that expires in 5 days
        _orgUserInviteTokenableFactory.CreateToken(orgUser).Returns(new OrgUserInviteTokenable(orgUser)
        {
            ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(5))
        });

        // TODO: figure out how to do this. Either have to instantiate the command with the fake data protection token factory
        // in constructor or maybe create a CustomizedBitAutoDataAttribute
        // Command must use fake data protection token factory for token validation so that our created tokens in
        // CreateNewToken are seen as valid in the command.
        // sutProvider.GetDependency<IDataProtectorTokenFactory<OrgUserInviteTokenable>>().Returns(_orgUserInviteTokenDataFactory);

        var newToken = CreateNewToken(orgUser);

        // Act
        var result = await sutProvider.Sut.AcceptOrgUserByTokenAsync(orgUser.Id, user, newToken, _userService);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(OrganizationUserStatusType.Accepted, result.Status);
        Assert.Equal(orgUser.Id, result.Id);
        Assert.Null(result.Email);
        Assert.Equal(user.Id, result.UserId);

        // Verify user email verified logic
        Assert.True(user.EmailVerified);
        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(
            Arg.Is<User>(u => u.Id == user.Id && u.Email == user.Email && user.EmailVerified == true));
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
