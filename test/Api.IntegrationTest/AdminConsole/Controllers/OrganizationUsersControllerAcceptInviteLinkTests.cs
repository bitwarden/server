using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUsersControllerAcceptInviteLinkTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private const string _invite = "opaque-invite-blob";

    private Organization _organization = null!;
    private string _ownerEmail = null!;

    public OrganizationUsersControllerAcceptInviteLinkTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IFeatureService>(featureService =>
        {
            featureService
                .IsEnabled(FeatureFlagKeys.GenerateInviteLink)
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
    public async Task AcceptInviteLink_WithValidRequest_ReturnsOk()
    {
        var createRequest = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = ["example.com"],
            Invite = _invite,
            SupportsConfirmation = false,
        };
        var createResponse = await _client.PostAsJsonAsync(
            $"/organizations/{_organization.Id}/invite-link", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<OrganizationInviteLinkResponseModel>();
        Assert.NotNull(created);

        var joinerEmail = $"integration-test{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(joinerEmail);
        var joinerClient = _factory.CreateClient();
        var joinerLoginHelper = new LoginHelper(_factory, joinerClient);
        await joinerLoginHelper.LoginAsync(joinerEmail);

        var acceptRequest = new AcceptOrganizationInviteLinkRequestModel { OrganizationId = created.OrganizationId, Code = created.Code };
        var response = await joinerClient.PostAsJsonAsync(
            "/organizations/users/invite-link/accept", acceptRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var userRepository = _factory.GetService<IUserRepository>();
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var joiner = await userRepository.GetByEmailAsync(joinerEmail);
        Assert.NotNull(joiner);

        var organizationUser = await organizationUserRepository.GetByOrganizationAsync(_organization.Id, joiner.Id);
        Assert.NotNull(organizationUser);
        Assert.Equal(OrganizationUserStatusType.Accepted, organizationUser.Status);
    }

    [Fact]
    public async Task AcceptInviteLink_WithAccountRecoveryAutoEnrollEnabled_EnrollsUserInAccountRecovery()
    {
        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        _organization.UseResetPassword = true;
        _organization.UsePolicies = true;
        await organizationRepository.ReplaceAsync(_organization);

        var policyRepository = _factory.GetService<IPolicyRepository>();
        var resetPasswordPolicy = new Policy
        {
            OrganizationId = _organization.Id,
            Type = PolicyType.ResetPassword,
            Enabled = true,
        };
        resetPasswordPolicy.SetDataModel(new ResetPasswordDataModel { AutoEnrollEnabled = true });
        await policyRepository.CreateAsync(resetPasswordPolicy);

        var createRequest = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = ["example.com"],
            Invite = _invite,
            SupportsConfirmation = false,
        };
        var createResponse = await _client.PostAsJsonAsync(
            $"/organizations/{_organization.Id}/invite-link", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<OrganizationInviteLinkResponseModel>();
        Assert.NotNull(created);

        var joinerEmail = $"integration-test{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(joinerEmail);
        var joinerClient = _factory.CreateClient();
        var joinerLoginHelper = new LoginHelper(_factory, joinerClient);
        await joinerLoginHelper.LoginAsync(joinerEmail);

        const string resetPasswordKey = "2.reset-password-key";
        var acceptRequest = new AcceptOrganizationInviteLinkRequestModel
        {
            OrganizationId = created.OrganizationId,
            Code = created.Code,
            ResetPasswordKey = resetPasswordKey,
        };
        var response = await joinerClient.PostAsJsonAsync(
            "/organizations/users/invite-link/accept", acceptRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var userRepository = _factory.GetService<IUserRepository>();
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var joiner = await userRepository.GetByEmailAsync(joinerEmail);
        Assert.NotNull(joiner);

        var organizationUser = await organizationUserRepository.GetByOrganizationAsync(_organization.Id, joiner.Id);
        Assert.NotNull(organizationUser);
        Assert.Equal(OrganizationUserStatusType.Accepted, organizationUser.Status);
        Assert.Equal(resetPasswordKey, organizationUser.ResetPasswordKey);
    }
}
