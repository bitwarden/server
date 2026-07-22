using System.Net;
using System.Text.Json;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

/// <summary>
/// Integration tests for the v2 UpdateOrganizationUser flow, exercised through the
/// <c>PUT organizations/{orgId}/users/{id}</c> endpoint with the <see cref="FeatureFlagKeys.ChangeMemberEmailNoMp"/>
/// flag enabled (which is what routes the controller to the v2 command).
/// </summary>
public class OrganizationUserControllerVNextTests : IAsyncLifetime
{
    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly LoginHelper _loginHelper;

    private Organization _organization = null!;
    private string _ownerEmail = null!;

    public OrganizationUserControllerVNextTests()
    {
        _factory = new ApiApplicationFactory();
        // The controller only routes to the v2 command when this flag is enabled.
        _factory.SubstituteService<IFeatureService>(featureService =>
            featureService.IsEnabled(FeatureFlagKeys.ChangeMemberEmailNoMp).Returns(true));
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"org-user-vnext-test-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail, passwordManagerSeats: 5, paymentMethod: PaymentMethodType.Card);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    [Fact]
    public async Task Put_WhenChangingRoleAndName_ReturnsNoContentAndPersistsBoth()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var (_, organizationUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);

        var request = new OrganizationUserUpdateRequestModel
        {
            Type = OrganizationUserType.Admin,
            Permissions = new Permissions(),
            Collections = [],
            Groups = [],
            Name = "Updated Name"
        };

        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{organizationUser.Id}", request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var updatedOrgUser = await _factory.GetService<IOrganizationUserRepository>().GetByIdAsync(organizationUser.Id);
        Assert.NotNull(updatedOrgUser);
        Assert.Equal(OrganizationUserType.Admin, updatedOrgUser.Type);

        var updatedUser = await _factory.GetService<IUserRepository>().GetByIdAsync(organizationUser.UserId!.Value);
        Assert.NotNull(updatedUser);
        Assert.Equal("Updated Name", updatedUser.Name);
    }

    [Fact]
    public async Task Put_WhenChangingEmailForClaimedMember_ReturnsNoContentAndPersistsEmail()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var (member, domain) = await CreateClaimedMemberWithoutMasterPasswordAsync();

        var newEmail = $"new-{Guid.NewGuid()}@{domain}";
        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{member.Id}", UpdateRequest(email: newEmail));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var updatedUser = await _factory.GetService<IUserRepository>().GetByIdAsync(member.UserId!.Value);
        Assert.NotNull(updatedUser);
        Assert.Equal(newEmail, updatedUser.Email, ignoreCase: true);
        Assert.True(updatedUser.EmailVerified);
    }

    [Fact]
    public async Task Put_WhenChangingEmailAndNameForClaimedMember_PersistsBoth()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var (member, domain) = await CreateClaimedMemberWithoutMasterPasswordAsync();

        var newEmail = $"new-{Guid.NewGuid()}@{domain}";
        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{member.Id}", UpdateRequest(email: newEmail, name: "Updated Name"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var updatedUser = await _factory.GetService<IUserRepository>().GetByIdAsync(member.UserId!.Value);
        Assert.NotNull(updatedUser);
        Assert.Equal(newEmail, updatedUser.Email, ignoreCase: true);
        Assert.Equal("Updated Name", updatedUser.Name);
    }

    [Fact]
    public async Task Put_WhenChangingEmailForUnclaimedMember_ReturnsNotClaimedProblemDetails()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        // No verified domain is created, so the member is not claimed by the organization.
        var memberEmail = $"unclaimed-{Guid.NewGuid()}@bitwarden.com";
        var (_, member) = await OrganizationTestHelpers.CreateUserWithoutMasterPasswordAsync(
            _factory, memberEmail, _organization.Id);

        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{member.Id}",
            UpdateRequest(email: $"new-{Guid.NewGuid()}@bitwarden.com"));

        await AssertEmailValidationProblemAsync(response, "member_not_claimed",
            "Cannot change the email of a member who is not claimed by the organization.");
    }

    [Fact]
    public async Task Put_WhenChangingEmailForMemberWithMasterPassword_ReturnsHasMasterPasswordProblemDetails()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        // CreateNewUserWithAccountAsync registers a real account, which has a master password.
        var (_, member) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);

        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{member.Id}",
            UpdateRequest(email: $"new-{Guid.NewGuid()}@bitwarden.com"));

        await AssertEmailValidationProblemAsync(response, "member_has_master_password",
            "Cannot change the email of a member who has a master password.");
    }

    [Fact]
    public async Task Put_WhenChangingEmailToUnverifiedDomain_ReturnsDomainNotClaimedProblemDetails()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var (member, _) = await CreateClaimedMemberWithoutMasterPasswordAsync();

        var unverifiedDomain = OrganizationTestHelpers.GenerateRandomDomain();
        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{member.Id}",
            UpdateRequest(email: $"new-{Guid.NewGuid()}@{unverifiedDomain}"));

        await AssertEmailValidationProblemAsync(response, "new_email_domain_not_claimed",
            "The new email address must be on a domain claimed by the organization.");
    }

    [Fact]
    public async Task Put_WhenChangingEmailToAddressAlreadyInUse_ReturnsAlreadyInUseProblemDetails()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var (member, domain) = await CreateClaimedMemberWithoutMasterPasswordAsync();

        // A second account already owns the target email (on the same verified domain, so validation passes
        // and the real ChangeEmailCommand's uniqueness check is what rejects it).
        var takenEmail = $"taken-{Guid.NewGuid()}@{domain}";
        await OrganizationTestHelpers.CreateUserWithoutMasterPasswordAsync(_factory, takenEmail, _organization.Id);

        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{member.Id}", UpdateRequest(email: takenEmail));

        await AssertEmailValidationProblemAsync(response, "email_already_in_use", "Email already in use.");
    }

    /// <summary>
    /// Creates a master-password-less member whose current email is on a verified organization domain, which is
    /// what makes them "claimed" and eligible for an admin email change.
    /// </summary>
    private async Task<(OrganizationUser Member, string Domain)> CreateClaimedMemberWithoutMasterPasswordAsync()
    {
        var domain = OrganizationTestHelpers.GenerateRandomDomain();
        _organization.UseOrganizationDomains = true;
        await _factory.GetService<IOrganizationRepository>().ReplaceAsync(_organization);
        await OrganizationTestHelpers.CreateVerifiedDomainAsync(_factory, _organization.Id, domain);

        var (_, member) = await OrganizationTestHelpers.CreateUserWithoutMasterPasswordAsync(
            _factory, $"member-{Guid.NewGuid()}@{domain}", _organization.Id);
        return (member, domain);
    }

    private static OrganizationUserUpdateRequestModel UpdateRequest(string? email = null, string? name = null) =>
        new()
        {
            Type = OrganizationUserType.User,
            Permissions = new Permissions(),
            Collections = [],
            Groups = [],
            Email = email,
            Name = name
        };

    private static async Task AssertEmailValidationProblemAsync(
        HttpResponseMessage response, string expectedType, string expectedDetail)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = problem.RootElement;
        Assert.Equal("validation_error", root.GetProperty("type").GetString());
        Assert.Equal("One or more validation errors occurred.", root.GetProperty("title").GetString());
        Assert.Equal(400, root.GetProperty("status").GetInt32());

        var emailErrors = root.GetProperty("errors").GetProperty("email");
        Assert.Equal(1, emailErrors.GetArrayLength());
        Assert.Equal(expectedType, emailErrors[0].GetProperty("type").GetString());
        Assert.Equal(expectedDetail, emailErrors[0].GetProperty("detail").GetString());
    }
}
