using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

/// <summary>
/// Integration tests for <see cref="Bit.Api.AdminConsole.Attributes.BindOrganizationAttribute"/>,
/// exercised through the GET reset-password-details endpoint which binds an Organization from the
/// <c>orgId</c> route parameter.
/// </summary>
public class OrganizationUsersControllerBindOrganizationTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly LoginHelper _loginHelper;

    private string _ownerEmail = null!;

    public OrganizationUsersControllerBindOrganizationTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"bind-org-test-{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(_ownerEmail);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetResetPasswordDetails_HappyPath_ReturnsOk()
    {
        // Arrange
        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail,
            passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);

        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        organization.UseResetPassword = true;
        await organizationRepository.ReplaceAsync(organization);

        await _loginHelper.LoginAsync(_ownerEmail);

        var (_, memberOrgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, organization.Id, OrganizationUserType.User);

        var orgUserRepository = _factory.GetService<IOrganizationUserRepository>();
        memberOrgUser.ResetPasswordKey = "encrypted-reset-password-key";
        await orgUserRepository.ReplaceAsync(memberOrgUser);

        // Act
        var response = await _client.GetAsync(
            $"organizations/{organization.Id}/users/{memberOrgUser.Id}/reset-password-details");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await organizationRepository.DeleteAsync(organization);
    }

    [Fact]
    public async Task GetResetPasswordDetails_OrgUserNotFound_ReturnsNotFound()
    {
        // Arrange — org exists and auth passes, but the org user ID in the path does not exist.
        // BindOrganizationAttribute successfully binds the org; the endpoint then throws
        // NotFoundException because the repository returns null for the unknown org user ID.
        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail,
            passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);

        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        organization.UseResetPassword = true;
        await organizationRepository.ReplaceAsync(organization);

        await _loginHelper.LoginAsync(_ownerEmail);

        // Act — use a random Guid that has no matching OrganizationUser row
        var response = await _client.GetAsync(
            $"organizations/{organization.Id}/users/{Guid.NewGuid()}/reset-password-details");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await organizationRepository.DeleteAsync(organization);
    }

    [Fact]
    public async Task GetResetPasswordDetails_OrgUserBelongsToDifferentOrg_ReturnsNotFound()
    {
        // Arrange — create two separate organizations
        var (org1, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail,
            passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);

        var secondOwnerEmail = $"bind-org-test-owner2-{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(secondOwnerEmail);

        var (org2, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: secondOwnerEmail,
            passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);

        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        org1.UseResetPassword = true;
        await organizationRepository.ReplaceAsync(org1);

        // Create a user in org2
        var (_, org2MemberOrgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, org2.Id, OrganizationUserType.User);

        // Log in as owner of org1 (who has ManageAccountRecovery for org1)
        await _loginHelper.LoginAsync(_ownerEmail);

        // Act — request org1's endpoint but pass an org user ID that belongs to org2
        var response = await _client.GetAsync(
            $"organizations/{org1.Id}/users/{org2MemberOrgUser.Id}/reset-password-details");

        // Assert — the org user's OrganizationId does not match org1, so NotFoundException is thrown
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await organizationRepository.DeleteAsync(org1);
        await organizationRepository.DeleteAsync(org2);
    }
}
