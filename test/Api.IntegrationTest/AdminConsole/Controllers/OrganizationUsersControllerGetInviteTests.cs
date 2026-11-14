using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.AdminConsole.AbilitiesCache;
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
using Bitwarden.Server.Sdk.Features;
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

    [Fact]
    public async Task GetInvite_WhenJoinerIsAlreadyAdminOfAFreeOrganization_ReturnsFreeAdminLimitError()
    {
        // Arrange
        var code = await CreateInviteLinkAsync(["example.com"]);
        await SetPlanTypeAsync(PlanType.Free);

        var (joinerClient, joinerEmail) = await CreateJoinerClientAsync();
        await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.Free, ownerEmail: joinerEmail);
        await CreatePendingEmailInvitationAsync(joinerEmail, OrganizationUserType.Admin);

        // Act
        var response = await joinerClient.PostAsJsonAsync(
            "/organizations/users/invite-link/invite", BuildRequest(_organization.Id, code));

        // Assert
        await ValidationProblemAssertions.AssertValidationProblemAsync(
            response, HttpStatusCode.BadRequest, "organizationId", "only_one_free_organization_admin_allowed");
    }

    [Fact]
    public async Task GetInvite_WhenProviderUserJoinsAutoConfirmOrganization_ReturnsProviderUsersCannotJoinError()
    {
        // Arrange
        var code = await CreateInviteLinkAsync(["example.com"]);
        await EnablePolicyAsync(PolicyType.AutomaticUserConfirmation);

        var (joinerClient, joinerEmail) = await CreateJoinerClientAsync();
        await AddJoinerToProviderAsync(joinerEmail);

        // Act
        var response = await joinerClient.PostAsJsonAsync(
            "/organizations/users/invite-link/invite", BuildRequest(_organization.Id, code));

        // Assert
        await ValidationProblemAssertions.AssertValidationProblemAsync(
            response, HttpStatusCode.BadRequest, "code", "provider_users_cannot_join");
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

    private async Task SetPlanTypeAsync(PlanType planType)
    {
        _organization.PlanType = planType;
        await _factory.GetService<IOrganizationRepository>().ReplaceAsync(_organization);
        await _factory.GetService<IOrganizationAbilityCacheService>().UpsertOrganizationAbilityAsync(_organization);
    }

    private async Task EnablePolicyAsync(PolicyType policyType)
    {
        _organization.UsePolicies = true;
        await _factory.GetService<IOrganizationRepository>().ReplaceAsync(_organization);

        // Policy enforcement reads UsePolicies from the organization ability cache.
        await _factory.GetService<IOrganizationAbilityCacheService>().UpsertOrganizationAbilityAsync(_organization);

        await _factory.GetService<IPolicyRepository>().CreateAsync(new Policy
        {
            OrganizationId = _organization.Id,
            Type = policyType,
            Enabled = true,
        });
    }

    // Creates a pending email invitation (Invited, no linked UserId), so the joiner joins with the invited role.
    private async Task CreatePendingEmailInvitationAsync(string joinerEmail, OrganizationUserType type) =>
        await _factory.GetService<IOrganizationUserRepository>().CreateAsync(new OrganizationUser
        {
            OrganizationId = _organization.Id,
            UserId = null,
            Email = joinerEmail,
            Type = type,
            Status = OrganizationUserStatusType.Invited,
        });

    private async Task AddJoinerToProviderAsync(string joinerEmail)
    {
        var joiner = await _factory.GetService<IUserRepository>().GetByEmailAsync(joinerEmail);
        Assert.NotNull(joiner);

        var provider = await _factory.GetService<IProviderRepository>().CreateAsync(new Provider
        {
            Name = "Integration Test Provider",
            BillingEmail = "billing@example.com",
            Type = ProviderType.Msp,
            Status = ProviderStatusType.Created,
            Enabled = true,
        });

        await _factory.GetService<IProviderUserRepository>().CreateAsync(new ProviderUser
        {
            ProviderId = provider.Id,
            UserId = joiner.Id,
            Type = ProviderUserType.ProviderAdmin,
            Status = ProviderUserStatusType.Confirmed,
            Key = Guid.NewGuid().ToString(),
        });
    }

    private static GetOrganizationInviteRequestModel BuildRequest(Guid organizationId, Guid code) =>
        new()
        {
            OrganizationId = organizationId,
            Code = code,
        };
}
