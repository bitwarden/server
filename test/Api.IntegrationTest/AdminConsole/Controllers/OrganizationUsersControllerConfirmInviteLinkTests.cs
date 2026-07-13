using System.Net;
using System.Text.Json;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUsersControllerConfirmInviteLinkTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private const string _validEncryptedKey =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";

    private Organization _organization = null!;
    private string _ownerEmail = null!;

    public OrganizationUsersControllerConfirmInviteLinkTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IFeatureService>(featureService =>
        {
            featureService
                .IsEnabled(FeatureFlagKeys.GenerateInviteLink)
                .Returns(true);
            featureService
                .IsEnabled(FeatureFlagKeys.InviteLinkAutoConfirm)
                .Returns(true);
        });
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"integration-test{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(
            _factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail,
            passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);

        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        _organization.UseInviteLinks = true;
        await organizationRepository.ReplaceAsync(_organization);

        await _loginHelper.LoginAsync(_ownerEmail);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ConfirmInviteLink_WithValidRequest_ReturnsOkAndConfirmsMembership()
    {
        // Arrange
        var code = await CreateInviteLinkAsync();
        var (joinerClient, joinerEmail) = await CreateJoinerClientAsync();

        // Act
        var response = await joinerClient.PostAsJsonAsync(
            "/organizations/users/invite-link/confirm", BuildConfirmRequest(_organization.Id, code));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var userRepository = _factory.GetService<IUserRepository>();
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var joiner = await userRepository.GetByEmailAsync(joinerEmail);
        Assert.NotNull(joiner);

        var organizationUser = await organizationUserRepository.GetByOrganizationAsync(_organization.Id, joiner.Id);
        Assert.NotNull(organizationUser);
        Assert.Equal(OrganizationUserStatusType.Confirmed, organizationUser.Status);
        Assert.Equal(_validEncryptedKey, organizationUser.Key);
    }

    [Fact]
    public async Task ConfirmInviteLink_WithUnknownCode_ReturnsNotFound()
    {
        // Arrange
        var (joinerClient, _) = await CreateJoinerClientAsync();

        // Act
        var response = await joinerClient.PostAsJsonAsync(
            "/organizations/users/invite-link/confirm", BuildConfirmRequest(_organization.Id, Guid.NewGuid()));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmInviteLink_WhenAlreadyConfirmed_ReturnsValidationProblemWithTypedCode()
    {
        // Arrange
        var code = await CreateInviteLinkAsync();
        var (joinerClient, _) = await CreateJoinerClientAsync();

        var firstResponse = await joinerClient.PostAsJsonAsync(
            "/organizations/users/invite-link/confirm", BuildConfirmRequest(_organization.Id, code));
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        // Act
        // Confirming again finds the already-confirmed membership and fails with a 400 validation problem.
        var secondResponse = await joinerClient.PostAsJsonAsync(
            "/organizations/users/invite-link/confirm", BuildConfirmRequest(_organization.Id, code));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);

        using var problem = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());
        var errorCode = problem.RootElement
            .GetProperty("errors")
            .GetProperty("code")[0]
            .GetProperty("type")
            .GetString();
        Assert.Equal("already_organization_member", errorCode);
    }

    private async Task<Guid> CreateInviteLinkAsync()
    {
        var createRequest = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = ["example.com"],
            Invite = _validEncryptedKey,
            SupportsConfirmation = true,
        };
        var createResponse = await _client.PostAsJsonAsync(
            $"/organizations/{_organization.Id}/invite-link", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<OrganizationInviteLinkResponseModel>();
        Assert.NotNull(created);
        return created.Code;
    }

    private async Task<(HttpClient Client, string Email)> CreateJoinerClientAsync()
    {
        var joinerEmail = $"integration-test{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(joinerEmail);
        var joinerClient = _factory.CreateClient();
        var joinerLoginHelper = new LoginHelper(_factory, joinerClient);
        await joinerLoginHelper.LoginAsync(joinerEmail);
        return (joinerClient, joinerEmail);
    }

    private static ConfirmOrganizationInviteLinkRequestModel BuildConfirmRequest(Guid organizationId, Guid code) =>
        new()
        {
            OrganizationId = organizationId,
            Code = code,
            OrgUserKey = _validEncryptedKey,
            DefaultUserCollectionName = _validEncryptedKey,
        };
}
