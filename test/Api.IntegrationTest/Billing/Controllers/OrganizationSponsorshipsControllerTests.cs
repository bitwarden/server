using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.Billing.Controllers;

/// <summary>
/// Integration tests for OrganizationSponsorshipsController, focusing on authorization checks
/// for the admin-initiated sponsorship endpoints.
/// </summary>
public class OrganizationSponsorshipsControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private Organization _organization = null!;
    private OrganizationUser _ownerOrgUser = null!;
    private string _ownerEmail = null!;

    public OrganizationSponsorshipsControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        // Create an Enterprise org (required for sponsorship features)
        _ownerEmail = $"sponsorship-test-owner-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        (_organization, _ownerOrgUser) = await OrganizationTestHelpers.SignUpAsync(
            _factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail,
            passwordManagerSeats: 5,
            paymentMethod: PaymentMethodType.Card);

        // Enable the AdminSponsoredFamilies feature on the org
        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        _organization.UseAdminSponsoredFamilies = true;
        await organizationRepository.ReplaceAsync(_organization);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Reproduces VULN-441: Any authenticated user (not a member of the org) can revoke
    /// admin-initiated sponsorships by calling DELETE /{sponsoringOrgId}/{friendlyName}/revoke.
    /// This test asserts the CORRECT behavior (should return Forbidden/Unauthorized),
    /// and will FAIL until the fix is applied.
    /// </summary>
    [Fact]
    public async Task AdminInitiatedRevokeSponsorship_AsNonMember_ReturnsForbidden()
    {
        // Arrange: Create a sponsorship directly in the DB for the org
        var sponsorship = await CreateAdminInitiatedSponsorshipAsync(
            _organization.Id, _ownerOrgUser.Id, "victim@example.com");

        // Create a completely separate user who is NOT a member of the org
        var attackerEmail = $"attacker-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(attackerEmail);
        await _loginHelper.LoginAsync(attackerEmail);

        // Act: The attacker tries to revoke the sponsorship
        var response = await _client.DeleteAsync(
            $"organization/sponsorship/{_organization.Id}/{Uri.EscapeDataString(sponsorship.FriendlyName!)}/revoke");

        // Assert: Should be rejected — non-members must not be able to revoke sponsorships
        Assert.True(
            response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized,
            $"Expected 401 or 403 but got {(int)response.StatusCode} {response.StatusCode}. " +
            "Non-org-members should not be able to revoke admin-initiated sponsorships.");

        // Verify the sponsorship still exists (was NOT deleted)
        var sponsorshipRepository = _factory.GetService<IOrganizationSponsorshipRepository>();
        var stillExists = await sponsorshipRepository.GetByIdAsync(sponsorship.Id);
        Assert.NotNull(stillExists);
        Assert.False(stillExists.ToDelete, "Sponsorship should not have been marked for deletion.");
    }

    /// <summary>
    /// Verifies that a regular member (User type, no special permissions) of the org
    /// also cannot revoke admin-initiated sponsorships.
    /// </summary>
    [Fact]
    public async Task AdminInitiatedRevokeSponsorship_AsRegularMember_ReturnsForbidden()
    {
        // Arrange: Create a sponsorship
        var sponsorship = await CreateAdminInitiatedSponsorshipAsync(
            _organization.Id, _ownerOrgUser.Id, "victim2@example.com");

        // Create a regular member of the org (User type, no ManageUsers permission)
        var (memberEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User,
            permissions: new Permissions { ManageUsers = false });

        await _loginHelper.LoginAsync(memberEmail);

        // Act
        var response = await _client.DeleteAsync(
            $"organization/sponsorship/{_organization.Id}/{Uri.EscapeDataString(sponsorship.FriendlyName!)}/revoke");

        // Assert: Regular members without ManageUsers should not be able to revoke
        Assert.True(
            response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized,
            $"Expected 401 or 403 but got {(int)response.StatusCode} {response.StatusCode}. " +
            "Regular org members without ManageUsers should not be able to revoke admin-initiated sponsorships.");

        // Verify the sponsorship still exists
        var sponsorshipRepository = _factory.GetService<IOrganizationSponsorshipRepository>();
        var stillExists = await sponsorshipRepository.GetByIdAsync(sponsorship.Id);
        Assert.NotNull(stillExists);
        Assert.False(stillExists.ToDelete, "Sponsorship should not have been marked for deletion.");
    }

    /// <summary>
    /// Verifies that an org Owner CAN revoke admin-initiated sponsorships (positive test).
    /// </summary>
    [Fact]
    public async Task AdminInitiatedRevokeSponsorship_AsOwner_Succeeds()
    {
        // Arrange: Create a sponsorship
        var sponsorship = await CreateAdminInitiatedSponsorshipAsync(
            _organization.Id, _ownerOrgUser.Id, "employee@example.com");

        await _loginHelper.LoginAsync(_ownerEmail);

        // Act
        var response = await _client.DeleteAsync(
            $"organization/sponsorship/{_organization.Id}/{Uri.EscapeDataString(sponsorship.FriendlyName!)}/revoke");

        // Assert: Owner should be able to revoke
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Verifies that an org Admin CAN revoke admin-initiated sponsorships.
    /// </summary>
    [Fact]
    public async Task AdminInitiatedRevokeSponsorship_AsAdmin_Succeeds()
    {
        // Arrange
        var sponsorship = await CreateAdminInitiatedSponsorshipAsync(
            _organization.Id, _ownerOrgUser.Id, "employee-admin@example.com");

        var (adminEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.Admin);

        await _loginHelper.LoginAsync(adminEmail);

        // Act
        var response = await _client.DeleteAsync(
            $"organization/sponsorship/{_organization.Id}/{Uri.EscapeDataString(sponsorship.FriendlyName!)}/revoke");

        // Assert: Admin should be able to revoke
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Verifies that a Custom user with ManageUsers permission CAN revoke admin-initiated sponsorships.
    /// </summary>
    [Fact]
    public async Task AdminInitiatedRevokeSponsorship_AsCustomWithManageUsers_Succeeds()
    {
        // Arrange
        var sponsorship = await CreateAdminInitiatedSponsorshipAsync(
            _organization.Id, _ownerOrgUser.Id, "employee-custom@example.com");

        var (customEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.Custom,
            permissions: new Permissions { ManageUsers = true });

        await _loginHelper.LoginAsync(customEmail);

        // Act
        var response = await _client.DeleteAsync(
            $"organization/sponsorship/{_organization.Id}/{Uri.EscapeDataString(sponsorship.FriendlyName!)}/revoke");

        // Assert: Custom user with ManageUsers should be able to revoke
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Reproduces the cross-org attack: user is admin of Org A but tries to revoke
    /// sponsorships of Org B (of which they are NOT a member).
    /// </summary>
    [Fact]
    public async Task AdminInitiatedRevokeSponsorship_AsAdminOfDifferentOrg_ReturnsForbidden()
    {
        // Arrange: Create a sponsorship on the target org
        var sponsorship = await CreateAdminInitiatedSponsorshipAsync(
            _organization.Id, _ownerOrgUser.Id, "cross-org-victim@example.com");

        // Create a different org and make the attacker its owner
        var attackerEmail = $"other-org-admin-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(attackerEmail);

        var (otherOrg, _) = await OrganizationTestHelpers.SignUpAsync(
            _factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: attackerEmail,
            name: "Attacker Org",
            billingEmail: attackerEmail,
            passwordManagerSeats: 5,
            paymentMethod: PaymentMethodType.Card);

        // Log in as the attacker (owner of otherOrg, NOT a member of _organization)
        await _loginHelper.LoginAsync(attackerEmail);

        // Act: Try to revoke a sponsorship on the target org
        var response = await _client.DeleteAsync(
            $"organization/sponsorship/{_organization.Id}/{Uri.EscapeDataString(sponsorship.FriendlyName!)}/revoke");

        // Assert: Should be rejected — being admin of another org doesn't grant access
        Assert.True(
            response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized,
            $"Expected 401 or 403 but got {(int)response.StatusCode} {response.StatusCode}. " +
            "Admin of a different org should not be able to revoke sponsorships of another org.");

        // Verify the sponsorship still exists
        var sponsorshipRepository = _factory.GetService<IOrganizationSponsorshipRepository>();
        var stillExists = await sponsorshipRepository.GetByIdAsync(sponsorship.Id);
        Assert.NotNull(stillExists);
        Assert.False(stillExists.ToDelete, "Sponsorship should not have been marked for deletion.");
    }

    #region ResendSponsorshipOffer authorization tests

    /// <summary>
    /// Verifies that a non-member cannot trigger sponsorship offer emails
    /// for an organization they don't belong to.
    /// </summary>
    [Fact]
    public async Task ResendSponsorshipOffer_AsNonMember_ReturnsForbidden()
    {
        // Arrange
        var sponsorship = await CreateAdminInitiatedSponsorshipAsync(
            _organization.Id, _ownerOrgUser.Id, "resend-victim@example.com");

        var attackerEmail = $"resend-attacker-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(attackerEmail);
        await _loginHelper.LoginAsync(attackerEmail);

        // Act
        var response = await _client.PostAsync(
            $"organization/sponsorship/{_organization.Id}/families-for-enterprise/resend?sponsoredFriendlyName={Uri.EscapeDataString(sponsorship.FriendlyName!)}",
            null);

        // Assert
        Assert.True(
            response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized,
            $"Expected 401 or 403 but got {(int)response.StatusCode} {response.StatusCode}. " +
            "Non-org-members should not be able to resend sponsorship offers.");
    }

    /// <summary>
    /// Verifies that a regular member without ManageUsers cannot resend sponsorship offers.
    /// </summary>
    [Fact]
    public async Task ResendSponsorshipOffer_AsRegularMember_ReturnsForbidden()
    {
        // Arrange
        var sponsorship = await CreateAdminInitiatedSponsorshipAsync(
            _organization.Id, _ownerOrgUser.Id, "resend-victim2@example.com");

        var (memberEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User,
            permissions: new Permissions { ManageUsers = false });

        await _loginHelper.LoginAsync(memberEmail);

        // Act
        var response = await _client.PostAsync(
            $"organization/sponsorship/{_organization.Id}/families-for-enterprise/resend?sponsoredFriendlyName={Uri.EscapeDataString(sponsorship.FriendlyName!)}",
            null);

        // Assert
        Assert.True(
            response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized,
            $"Expected 401 or 403 but got {(int)response.StatusCode} {response.StatusCode}. " +
            "Regular org members without ManageUsers should not be able to resend sponsorship offers.");
    }

    /// <summary>
    /// Verifies that an admin of a different org cannot resend sponsorship offers
    /// for the target org (cross-org attack).
    /// </summary>
    [Fact]
    public async Task ResendSponsorshipOffer_AsAdminOfDifferentOrg_ReturnsForbidden()
    {
        // Arrange
        var sponsorship = await CreateAdminInitiatedSponsorshipAsync(
            _organization.Id, _ownerOrgUser.Id, "resend-cross-org@example.com");

        var attackerEmail = $"resend-other-org-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(attackerEmail);

        await OrganizationTestHelpers.SignUpAsync(
            _factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: attackerEmail,
            name: "Resend Attacker Org",
            billingEmail: attackerEmail,
            passwordManagerSeats: 5,
            paymentMethod: PaymentMethodType.Card);

        await _loginHelper.LoginAsync(attackerEmail);

        // Act
        var response = await _client.PostAsync(
            $"organization/sponsorship/{_organization.Id}/families-for-enterprise/resend?sponsoredFriendlyName={Uri.EscapeDataString(sponsorship.FriendlyName!)}",
            null);

        // Assert
        Assert.True(
            response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized,
            $"Expected 401 or 403 but got {(int)response.StatusCode} {response.StatusCode}. " +
            "Admin of a different org should not be able to resend sponsorship offers for another org.");
    }

    /// <summary>
    /// Verifies that an org Owner CAN resend sponsorship offers.
    /// Note: The endpoint may still return a non-200 due to downstream email/policy logic,
    /// but crucially it should NOT return 401/403.
    /// </summary>
    [Fact]
    public async Task ResendSponsorshipOffer_AsOwner_IsNotForbidden()
    {
        // Arrange
        var sponsorship = await CreateAdminInitiatedSponsorshipAsync(
            _organization.Id, _ownerOrgUser.Id, "resend-employee@example.com");

        await _loginHelper.LoginAsync(_ownerEmail);

        // Act
        var response = await _client.PostAsync(
            $"organization/sponsorship/{_organization.Id}/families-for-enterprise/resend?sponsoredFriendlyName={Uri.EscapeDataString(sponsorship.FriendlyName!)}",
            null);

        // Assert: Should pass authorization (may fail downstream for other reasons, but not 401/403)
        Assert.True(
            response.StatusCode is not HttpStatusCode.Forbidden and not HttpStatusCode.Unauthorized,
            $"Expected to pass authorization but got {(int)response.StatusCode} {response.StatusCode}.");
    }

    /// <summary>
    /// Verifies that an org Admin CAN resend sponsorship offers.
    /// </summary>
    [Fact]
    public async Task ResendSponsorshipOffer_AsAdmin_IsNotForbidden()
    {
        // Arrange
        var sponsorship = await CreateAdminInitiatedSponsorshipAsync(
            _organization.Id, _ownerOrgUser.Id, "resend-admin@example.com");

        var (adminEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.Admin);

        await _loginHelper.LoginAsync(adminEmail);

        // Act
        var response = await _client.PostAsync(
            $"organization/sponsorship/{_organization.Id}/families-for-enterprise/resend?sponsoredFriendlyName={Uri.EscapeDataString(sponsorship.FriendlyName!)}",
            null);

        // Assert
        Assert.True(
            response.StatusCode is not HttpStatusCode.Forbidden and not HttpStatusCode.Unauthorized,
            $"Expected to pass authorization but got {(int)response.StatusCode} {response.StatusCode}.");
    }

    /// <summary>
    /// Verifies that a Custom user with ManageUsers CAN resend sponsorship offers.
    /// </summary>
    [Fact]
    public async Task ResendSponsorshipOffer_AsCustomWithManageUsers_IsNotForbidden()
    {
        // Arrange
        var sponsorship = await CreateAdminInitiatedSponsorshipAsync(
            _organization.Id, _ownerOrgUser.Id, "resend-custom@example.com");

        var (customEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.Custom,
            permissions: new Permissions { ManageUsers = true });

        await _loginHelper.LoginAsync(customEmail);

        // Act
        var response = await _client.PostAsync(
            $"organization/sponsorship/{_organization.Id}/families-for-enterprise/resend?sponsoredFriendlyName={Uri.EscapeDataString(sponsorship.FriendlyName!)}",
            null);

        // Assert
        Assert.True(
            response.StatusCode is not HttpStatusCode.Forbidden and not HttpStatusCode.Unauthorized,
            $"Expected to pass authorization but got {(int)response.StatusCode} {response.StatusCode}.");
    }

    #endregion

    /// <summary>
    /// Helper to create an admin-initiated sponsorship directly in the DB,
    /// bypassing the command layer (which has its own auth checks).
    /// </summary>
    private async Task<OrganizationSponsorship> CreateAdminInitiatedSponsorshipAsync(
        Guid sponsoringOrgId, Guid sponsoringOrgUserId, string friendlyName)
    {
        var sponsorshipRepository = _factory.GetService<IOrganizationSponsorshipRepository>();

        var sponsorship = new OrganizationSponsorship
        {
            SponsoringOrganizationId = sponsoringOrgId,
            SponsoringOrganizationUserId = sponsoringOrgUserId,
            FriendlyName = friendlyName,
            OfferedToEmail = friendlyName,
            PlanSponsorshipType = PlanSponsorshipType.FamiliesForEnterprise,
            IsAdminInitiated = true,
            ToDelete = false,
        };
        sponsorship.SetNewId();

        await sponsorshipRepository.CreateAsync(sponsorship);
        return sponsorship;
    }
}
