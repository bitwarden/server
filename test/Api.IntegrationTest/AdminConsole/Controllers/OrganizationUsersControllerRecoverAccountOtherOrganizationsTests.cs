using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bitwarden.Server.Sdk.Features;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

/// <summary>
/// Recovering an account removes the user from every other organization they belong to,
/// except where removing them would leave that organization without a confirmed owner.
/// </summary>
public class OrganizationUsersControllerRecoverAccountOtherOrganizationsTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IFeatureService _featureService;

    private Organization _recoveringOrganization = null!;

    public OrganizationUsersControllerRecoverAccountOtherOrganizationsTests(ApiApplicationFactory apiFactory)
    {
        _factory = apiFactory;
        _factory.SubstituteService<IFeatureService>(_ => { });
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _featureService = _factory.GetService<IFeatureService>();
    }

    public async Task InitializeAsync()
    {
        var ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(ownerEmail);

        (_recoveringOrganization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually,
            ownerEmail: ownerEmail, passwordManagerSeats: 5, paymentMethod: PaymentMethodType.Card);

        _recoveringOrganization.UseResetPassword = true;
        _recoveringOrganization.UsePolicies = true;
        await _factory.GetService<IOrganizationRepository>().ReplaceAsync(_recoveringOrganization);

        await _factory.GetService<IPolicyRepository>().CreateAsync(new Policy
        {
            OrganizationId = _recoveringOrganization.Id,
            Type = PolicyType.ResetPassword,
            Enabled = true,
            Data = "{}"
        });

        await _loginHelper.LoginAsync(ownerEmail);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task RecoverAccount_RemovesUserFromOtherOrganizations_ExceptWhereSoleOwner()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.AccountRecoveryMultiOrgCheck).Returns(true);

        var (targetEmail, targetOrganizationUser) = await CreateRecoverableUserAsync();
        var (_, soleOwnerOrganizationUser) = await OrganizationTestHelpers.SignUpAsync(_factory,
            ownerEmail: targetEmail, billingEmail: targetEmail);
        var otherOrganization = await CreateOrganizationWithOtherOwnerAsync();
        var otherOrganizationUser = await OrganizationTestHelpers.CreateUserAsync(_factory, otherOrganization.Id,
            targetEmail, OrganizationUserType.Admin);

        // Act
        var response = await RecoverAccountAsync(targetOrganizationUser);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var membershipIds = await GetMembershipIdsAsync(targetOrganizationUser.UserId!.Value);
        Assert.Contains(targetOrganizationUser.Id, membershipIds);
        Assert.Contains(soleOwnerOrganizationUser.Id, membershipIds);
        Assert.DoesNotContain(otherOrganizationUser.Id, membershipIds);
    }

    [Fact]
    public async Task RecoverAccount_FlagDisabled_KeepsUserInOtherOrganizations()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.AccountRecoveryMultiOrgCheck).Returns(false);

        var (targetEmail, targetOrganizationUser) = await CreateRecoverableUserAsync();
        var otherOrganization = await CreateOrganizationWithOtherOwnerAsync();
        var otherOrganizationUser = await OrganizationTestHelpers.CreateUserAsync(_factory, otherOrganization.Id,
            targetEmail, OrganizationUserType.Admin);

        // Act
        var response = await RecoverAccountAsync(targetOrganizationUser);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var membershipIds = await GetMembershipIdsAsync(targetOrganizationUser.UserId!.Value);
        Assert.Contains(targetOrganizationUser.Id, membershipIds);
        Assert.Contains(otherOrganizationUser.Id, membershipIds);
    }

    private async Task<(string Email, OrganizationUser OrganizationUser)> CreateRecoverableUserAsync()
    {
        var (email, organizationUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _recoveringOrganization.Id, OrganizationUserType.User);

        organizationUser.ResetPasswordKey = "encrypted-reset-password-key";
        await _factory.GetService<IOrganizationUserRepository>().ReplaceAsync(organizationUser);

        return (email, organizationUser);
    }

    private async Task<Organization> CreateOrganizationWithOtherOwnerAsync()
    {
        var ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(ownerEmail);

        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            ownerEmail: ownerEmail, billingEmail: ownerEmail);
        return organization;
    }

    private Task<HttpResponseMessage> RecoverAccountAsync(OrganizationUser organizationUser) =>
        _client.PutAsJsonAsync(
            $"organizations/{_recoveringOrganization.Id}/users/{organizationUser.Id}/recover-account",
            new OrganizationUserResetPasswordRequestModel
            {
                ResetMasterPassword = true,
                NewMasterPasswordHash = "new-master-password-hash",
                Key = "2.AAECAwQFBgcICQoLDA0ODw==|QmFzZTY0UGFydA==|AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8="
            });

    private async Task<IEnumerable<Guid>> GetMembershipIdsAsync(Guid userId) =>
        (await _factory.GetService<IOrganizationUserRepository>().GetManyByUserAsync(userId)).Select(ou => ou.Id);
}
