using System.Net;
using Bit.Api.AdminConsole.Authorization;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Kdf;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.Models.Api;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUsersControllerPutRecoverAccountTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private static readonly KdfRequestModel _defaultKdfRequest =
        new() { KdfType = KdfType.PBKDF2_SHA256, Iterations = KdfConstants.PBKDF2_ITERATIONS.Default };

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private Organization _organization = null!;
    private string _ownerEmail = null!;

    public OrganizationUsersControllerPutRecoverAccountTests(ApiApplicationFactory apiFactory)
    {
        _factory = apiFactory;
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"reset-password-test-{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail, passwordManagerSeats: 5, paymentMethod: PaymentMethodType.Card);

        // Enable reset password and policies for the organization
        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        _organization.UseResetPassword = true;
        _organization.UsePolicies = true;
        await organizationRepository.ReplaceAsync(_organization);

        // Enable the ResetPassword policy
        var policyRepository = _factory.GetService<IPolicyRepository>();
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = _organization.Id,
            Type = PolicyType.ResetPassword,
            Enabled = true,
            Data = "{}"
        });
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Helper method to set the ResetPasswordKey on an organization user, which is required for account recovery
    /// </summary>
    private async Task SetResetPasswordKeyAsync(OrganizationUser orgUser)
    {
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        orgUser.ResetPasswordKey = "encrypted-reset-password-key";
        await organizationUserRepository.ReplaceAsync(orgUser);
    }

    [Fact]
    public async Task PutRecoverAccount_WithLegacyPayload_UpdatesUserKeyAndForcesPasswordReset()
    {
        // Arrange
        var (ownerEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Owner);
        await _loginHelper.LoginAsync(ownerEmail);

        var (targetEmail, targetOrgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);
        await SetResetPasswordKeyAsync(targetOrgUser);

        var userRepository = _factory.GetService<IUserRepository>();
        var passwordHasher = _factory.GetService<IPasswordHasher<User>>();
        var userBefore = await userRepository.GetByEmailAsync(targetEmail);
        Assert.NotNull(userBefore);

        const string newMasterPasswordHash = "new-master-password-hash";
        var resetPasswordRequest = new OrganizationUserResetPasswordRequestModel
        {
            ResetMasterPassword = true,
            NewMasterPasswordHash = newMasterPasswordHash,
            Key = "encrypted-recovery-key"
        };

        // Act
        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{targetOrgUser.Id}/recover-account",
            resetPasswordRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var userAfter = await userRepository.GetByEmailAsync(targetEmail);
        Assert.NotNull(userAfter);
        Assert.Equal("encrypted-recovery-key", userAfter.Key);
        Assert.True(userAfter.ForcePasswordReset);
        Assert.Equal(PasswordVerificationResult.Success,
            passwordHasher.VerifyHashedPassword(userAfter, userAfter.MasterPassword!, newMasterPasswordHash));
        Assert.NotEqual(userBefore.SecurityStamp, userAfter.SecurityStamp);
        Assert.True(userAfter.LastPasswordChangeDate > userBefore.RevisionDate);
        Assert.True(userAfter.RevisionDate > userBefore.RevisionDate);
    }

    [Fact]
    public async Task PutRecoverAccount_WithUnlockAndAuthenticationData_UpdatesUserKeyAndForcesPasswordReset()
    {
        // Arrange
        var (ownerEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Owner);
        await _loginHelper.LoginAsync(ownerEmail);

        var (targetEmail, targetOrgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);
        await SetResetPasswordKeyAsync(targetOrgUser);

        var userRepository = _factory.GetService<IUserRepository>();
        var passwordHasher = _factory.GetService<IPasswordHasher<User>>();
        var userBefore = await userRepository.GetByEmailAsync(targetEmail);
        Assert.NotNull(userBefore);

        // Salt must match the user's existing salt; for a freshly registered user this falls back to the lowercased email.
        var salt = targetEmail.ToLowerInvariant().Trim();
        const string newWrappedUserKey =
            "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";
        const string newMasterPasswordHash = "new-master-password-hash";
        var resetPasswordRequest = new OrganizationUserResetPasswordRequestModel
        {
            ResetMasterPassword = true,
            UnlockData = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = _defaultKdfRequest,
                MasterKeyWrappedUserKey = newWrappedUserKey,
                Salt = salt
            },
            AuthenticationData = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = _defaultKdfRequest,
                MasterPasswordAuthenticationHash = newMasterPasswordHash,
                Salt = salt
            }
        };

        // Act
        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{targetOrgUser.Id}/recover-account",
            resetPasswordRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var userAfter = await userRepository.GetByEmailAsync(targetEmail);
        Assert.NotNull(userAfter);
        Assert.Equal(newWrappedUserKey, userAfter.Key);
        Assert.True(userAfter.ForcePasswordReset);
        Assert.Equal(PasswordVerificationResult.Success,
            passwordHasher.VerifyHashedPassword(userAfter, userAfter.MasterPassword!, newMasterPasswordHash));
        Assert.NotEqual(userBefore.SecurityStamp, userAfter.SecurityStamp);
        Assert.True(userAfter.LastPasswordChangeDate > userBefore.RevisionDate);
        Assert.True(userAfter.RevisionDate > userBefore.RevisionDate);
    }

    [Fact]
    public async Task PutRecoverAccount_AsLowerRole_CannotRecoverHigherRole()
    {
        // Arrange
        var (adminEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Admin);
        await _loginHelper.LoginAsync(adminEmail);

        var (_, targetOwnerOrgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.Owner);
        await SetResetPasswordKeyAsync(targetOwnerOrgUser);

        var resetPasswordRequest = new OrganizationUserResetPasswordRequestModel
        {
            ResetMasterPassword = true,
            NewMasterPasswordHash = "new-master-password-hash",
            Key = "encrypted-recovery-key"
        };

        // Act
        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{targetOwnerOrgUser.Id}/recover-account",
            resetPasswordRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var model = await response.Content.ReadFromJsonAsync<ErrorResponseModel>();
        Assert.Contains(RecoverAccountAuthorizationHandler.FailureReason, model.Message);
    }

    [Fact]
    public async Task PutRecoverAccount_CannotRecoverProviderAccount()
    {
        // Arrange - Create owner who will try to recover the provider account
        var (ownerEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Owner);
        await _loginHelper.LoginAsync(ownerEmail);

        // Create a user who is also a provider user
        var (targetUserEmail, targetOrgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);
        await SetResetPasswordKeyAsync(targetOrgUser);

        // Add the target user as a provider user to a different provider
        var providerRepository = _factory.GetService<IProviderRepository>();
        var providerUserRepository = _factory.GetService<IProviderUserRepository>();
        var userRepository = _factory.GetService<IUserRepository>();

        var provider = await providerRepository.CreateAsync(new Provider
        {
            Name = "Test Provider",
            BusinessName = "Test Provider Business",
            BillingEmail = "provider@example.com",
            Type = ProviderType.Msp,
            Status = ProviderStatusType.Created,
            Enabled = true
        });

        var targetUser = await userRepository.GetByEmailAsync(targetUserEmail);
        Assert.NotNull(targetUser);

        await providerUserRepository.CreateAsync(new ProviderUser
        {
            ProviderId = provider.Id,
            UserId = targetUser.Id,
            Status = ProviderUserStatusType.Confirmed,
            Type = ProviderUserType.ProviderAdmin
        });

        var resetPasswordRequest = new OrganizationUserResetPasswordRequestModel
        {
            ResetMasterPassword = true,
            NewMasterPasswordHash = "new-master-password-hash",
            Key = "encrypted-recovery-key"
        };

        // Act
        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{targetOrgUser.Id}/recover-account",
            resetPasswordRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var model = await response.Content.ReadFromJsonAsync<ErrorResponseModel>();
        Assert.Equal(RecoverAccountAuthorizationHandler.ProviderFailureReason, model.Message);
    }
}
