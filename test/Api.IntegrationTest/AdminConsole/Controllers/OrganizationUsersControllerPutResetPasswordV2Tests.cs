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
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.Models.Api;
using Bit.Core.Repositories;
using Bit.Test.Common.Constants;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUsersControllerPutResetPasswordV2Tests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private Organization _organization = null!;
    private string _ownerEmail = null!;

    public OrganizationUsersControllerPutResetPasswordV2Tests(ApiApplicationFactory apiFactory)
    {
        _factory = apiFactory;
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"reset-password-v2-test-{Guid.NewGuid()}@example.com";
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

    private async Task SetResetPasswordKeyAsync(OrganizationUser orgUser)
    {
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        orgUser.ResetPasswordKey = "encrypted-reset-password-key";
        await organizationUserRepository.ReplaceAsync(orgUser);
    }

    private async Task<OrganizationUserResetPasswordV2RequestModel> CreateV2RequestForUserAsync(string targetEmail)
    {
        var userRepository = _factory.GetService<IUserRepository>();
        var targetUser = await userRepository.GetByEmailAsync(targetEmail);
        Assert.NotNull(targetUser);

        var kdf = new KdfRequestModel
        {
            KdfType = targetUser.Kdf,
            Iterations = targetUser.KdfIterations,
            Memory = targetUser.KdfMemory,
            Parallelism = targetUser.KdfParallelism
        };
        var salt = targetUser.GetMasterPasswordSalt();

        return new OrganizationUserResetPasswordV2RequestModel
        {
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = "new-master-password-hash",
                Salt = salt
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = TestEncryptionConstants.AES256_CBC_HMAC_Encstring,
                Salt = salt
            }
        };
    }

    [Fact]
    public async Task PutResetPassword_AsHigherRole_CanRecoverLowerRole()
    {
        // Arrange
        var (ownerEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Owner);
        await _loginHelper.LoginAsync(ownerEmail);

        var (targetUserEmail, targetOrgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);
        await SetResetPasswordKeyAsync(targetOrgUser);

        var request = await CreateV2RequestForUserAsync(targetUserEmail);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"v2/organizations/{_organization.Id}/users/{targetOrgUser.Id}/reset-password",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PutResetPassword_AsLowerRole_CannotRecoverHigherRole()
    {
        // Arrange
        var (adminEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Admin);
        await _loginHelper.LoginAsync(adminEmail);

        var (targetOwnerEmail, targetOwnerOrgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.Owner);
        await SetResetPasswordKeyAsync(targetOwnerOrgUser);

        var request = await CreateV2RequestForUserAsync(targetOwnerEmail);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"v2/organizations/{_organization.Id}/users/{targetOwnerOrgUser.Id}/reset-password",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var model = await response.Content.ReadFromJsonAsync<ErrorResponseModel>();
        Assert.Contains(RecoverAccountAuthorizationHandler.FailureReason, model!.Message);
    }

    [Fact]
    public async Task PutResetPassword_CannotRecoverProviderAccount()
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

        var request = await CreateV2RequestForUserAsync(targetUserEmail);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"v2/organizations/{_organization.Id}/users/{targetOrgUser.Id}/reset-password",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var model = await response.Content.ReadFromJsonAsync<ErrorResponseModel>();
        Assert.Equal(RecoverAccountAuthorizationHandler.ProviderFailureReason, model!.Message);
    }

    [Fact]
    public async Task PutResetPassword_KdfMismatch_ReturnsBadRequest()
    {
        // Arrange
        var (ownerEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Owner);
        await _loginHelper.LoginAsync(ownerEmail);

        var (targetUserEmail, targetOrgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);
        await SetResetPasswordKeyAsync(targetOrgUser);

        var userRepository = _factory.GetService<IUserRepository>();
        var targetUser = await userRepository.GetByEmailAsync(targetUserEmail);
        Assert.NotNull(targetUser);

        // User is PBKDF2 but we send Argon2id
        var mismatchedKdf = new KdfRequestModel
        {
            KdfType = KdfType.Argon2id,
            Iterations = 3,
            Memory = 64,
            Parallelism = 4
        };
        var salt = targetUser.GetMasterPasswordSalt();
        var request = new OrganizationUserResetPasswordV2RequestModel
        {
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = mismatchedKdf,
                MasterPasswordAuthenticationHash = "new-master-password-hash",
                Salt = salt
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = mismatchedKdf,
                MasterKeyWrappedUserKey = TestEncryptionConstants.AES256_CBC_HMAC_Encstring,
                Salt = salt
            }
        };

        // Act
        var response = await _client.PutAsJsonAsync(
            $"v2/organizations/{_organization.Id}/users/{targetOrgUser.Id}/reset-password",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
