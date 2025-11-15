using System.Net;
using Bit.Api.AdminConsole.Authorization;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Models.Request.Organizations;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Repositories;
using Bit.Core.Services;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUsersControllerPutResetPasswordTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private Organization _organization = null!;
    private string _ownerEmail = null!;

    public OrganizationUsersControllerPutResetPasswordTests(ApiApplicationFactory apiFactory)
    {
        _factory = apiFactory;
        _factory.SubstituteService<IFeatureService>(featureService =>
        {
            featureService
                .IsEnabled(FeatureFlagKeys.AccountRecoveryCommand)
                .Returns(true);
        });
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
    public async Task PutResetPassword_AsHigherRole_CanRecoverLowerRole()
    {
        // Arrange
        var (ownerEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Owner);
        await _loginHelper.LoginAsync(ownerEmail);

        var (_, targetOrgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);
        await SetResetPasswordKeyAsync(targetOrgUser);

        var resetPasswordRequest = new OrganizationUserResetPasswordRequestModel
        {
            NewMasterPasswordHash = "new-master-password-hash",
            Key = "encrypted-recovery-key"
        };

        // Act
        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{targetOrgUser.Id}/reset-password",
            resetPasswordRequest);

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

        var (_, targetOwnerOrgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.Owner);
        await SetResetPasswordKeyAsync(targetOwnerOrgUser);

        var resetPasswordRequest = new OrganizationUserResetPasswordRequestModel
        {
            NewMasterPasswordHash = "new-master-password-hash",
            Key = "encrypted-recovery-key"
        };

        // Act
        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{targetOwnerOrgUser.Id}/reset-password",
            resetPasswordRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var model = await response.Content.ReadFromJsonAsync<ErrorResponseModel>();
        Assert.Contains(RecoverAccountAuthorizationHandler.FailureReason, model.Message);
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

        var resetPasswordRequest = new OrganizationUserResetPasswordRequestModel
        {
            NewMasterPasswordHash = "new-master-password-hash",
            Key = "encrypted-recovery-key"
        };

        // Act
        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{targetOrgUser.Id}/reset-password",
            resetPasswordRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var model = await response.Content.ReadFromJsonAsync<ErrorResponseModel>();
        Assert.Equal(RecoverAccountAuthorizationHandler.ProviderFailureReason, model.Message);
    }
}
