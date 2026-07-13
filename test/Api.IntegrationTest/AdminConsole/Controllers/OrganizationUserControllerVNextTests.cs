using System.Net;
using System.Text.Json;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
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
        // Arrange
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

        // Act
        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{organizationUser.Id}", request);

        // Assert: the v2 command returns 204 No Content on success (v1 returns 200 OK).
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // The org-user role change is persisted.
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var updatedOrgUser = await organizationUserRepository.GetByIdAsync(organizationUser.Id);
        Assert.NotNull(updatedOrgUser);
        Assert.Equal(OrganizationUserType.Admin, updatedOrgUser.Type);

        // The account name change is persisted.
        var userRepository = _factory.GetService<IUserRepository>();
        var updatedUser = await userRepository.GetByIdAsync(organizationUser.UserId!.Value);
        Assert.NotNull(updatedUser);
        Assert.Equal("Updated Name", updatedUser.Name);
    }

    [Fact]
    public async Task Put_WhenChangingEmailForUnclaimedMember_ReturnsNotClaimedProblemDetails()
    {
        // Arrange
        await _loginHelper.LoginAsync(_ownerEmail);

        // A member with no master password (so the master-password guard is not what trips first) who is not
        // claimed by the organization (no verified domain was created), so the email change is rejected as
        // "member not claimed".
        var memberEmail = $"unclaimed-{Guid.NewGuid()}@bitwarden.com";
        var (_, organizationUser) = await OrganizationTestHelpers.CreateUserWithoutMasterPasswordAsync(
            _factory, memberEmail, _organization.Id);

        var request = new OrganizationUserUpdateRequestModel
        {
            Type = OrganizationUserType.User,
            Permissions = new Permissions(),
            Collections = [],
            Groups = [],
            Email = $"new-{Guid.NewGuid()}@bitwarden.com"
        };

        // Act
        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{organizationUser.Id}", request);

        // Assert: a 400 RFC 7807 validation problem keyed on "email".
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = problem.RootElement;
        Assert.Equal("validation_error", root.GetProperty("type").GetString());
        Assert.Equal("One or more validation errors occurred.", root.GetProperty("title").GetString());
        Assert.Equal(400, root.GetProperty("status").GetInt32());

        var emailErrors = root.GetProperty("errors").GetProperty("email");
        Assert.Equal(1, emailErrors.GetArrayLength());

        var error = emailErrors[0];
        Assert.Equal("member_not_claimed", error.GetProperty("type").GetString());
        Assert.Equal(
            "Cannot change the email of a member who is not claimed by the organization.",
            error.GetProperty("detail").GetString());
    }
}
