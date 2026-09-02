using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUsersControllerGetInviteTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private const string _validEncryptedKey =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";

    private Organization _organization = null!;
    private string _ownerEmail = null!;

    public OrganizationUsersControllerGetInviteTests(ApiApplicationFactory factory)
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

        // The endpoint reads Enabled/UseInviteLinks from the organization ability cache, so refresh it
        // to reflect the invite links being enabled above.
        await _factory.GetService<IOrganizationAbilityCacheService>()
            .UpsertOrganizationAbilityAsync(_organization);

        await _loginHelper.LoginAsync(_ownerEmail);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetInvite_WithValidRequest_ReturnsOkAndInvite()
    {
        // Arrange
        var code = await CreateInviteLinkAsync(["example.com"]);
        var (joinerClient, _) = await CreateJoinerClientAsync();

        // Act
        var response = await joinerClient.PostAsJsonAsync(
            "/organizations/users/invite-link/invite", BuildRequest(_organization.Id, code));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<OrganizationInviteResponseModel>();
        Assert.NotNull(result);
        Assert.Equal(_validEncryptedKey, result.Invite);
    }

    private async Task<Guid> CreateInviteLinkAsync(string[] allowedDomains)
    {
        var createRequest = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = allowedDomains,
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

    private static GetOrganizationInviteRequestModel BuildRequest(Guid organizationId, Guid code) =>
        new()
        {
            OrganizationId = organizationId,
            Code = code,
        };
}
