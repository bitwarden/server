using System.Net;
using System.Net.Http.Json;
using Bit.Api.AdminConsole.Public.Models.Request;
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

namespace Bit.Api.IntegrationTest.AdminConsole.Public.Controllers;

public class OrganizationControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    // These will get set in `InitializeAsync` which is ran before all tests
    private Organization _organization = null!;
    private string _ownerEmail = null!;

    public OrganizationControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        // The substitution must be registered before the host is built; each test sets the flag values it needs
        _factory.SubstituteService<IFeatureService>(_ => { });
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        // Create the owner account
        _ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        // Create the organization
        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail, passwordManagerSeats: 10, paymentMethod: PaymentMethodType.Card);

        // Authorize with the organization api key
        await _loginHelper.LoginWithOrganizationApiKeyAsync(_organization.Id);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Import_InviteUsersAfterProvisioningDisabled_CreatesStagedUsers()
    {
        _factory.GetService<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM34423StagedStatus)
            .Returns(true);

        var email1 = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        var email2 = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        var request = new OrganizationImportRequestModel
        {
            Groups = [],
            Members =
            [
                new OrganizationImportRequestModel.OrganizationImportMemberRequestModel { Email = email1, ExternalId = "external-1" },
                new OrganizationImportRequestModel.OrganizationImportMemberRequestModel { Email = email2, ExternalId = "external-2" }
            ],
            OverwriteExisting = false,
            InviteUsersAfterProvisioning = false
        };

        var response = await _client.PostAsJsonAsync("/public/organization/import", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var organizationUsers = await organizationUserRepository.GetManyByOrganizationAsync(_organization.Id, null);

        var stagedUser1 = Assert.Single(organizationUsers, ou => ou.Email == email1);
        Assert.Equal(OrganizationUserStatusType.Staged, stagedUser1.Status);
        Assert.Equal("external-1", stagedUser1.ExternalId);
        Assert.Null(stagedUser1.UserId);

        var stagedUser2 = Assert.Single(organizationUsers, ou => ou.Email == email2);
        Assert.Equal(OrganizationUserStatusType.Staged, stagedUser2.Status);
        Assert.Equal("external-2", stagedUser2.ExternalId);
        Assert.Null(stagedUser2.UserId);
    }

    [Fact]
    public async Task Import_InviteUsersAfterProvisioningOmitted_InvitesUsers()
    {
        _factory.GetService<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM34423StagedStatus)
            .Returns(true);

        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";

        // Omit inviteUsersAfterProvisioning entirely - a missing value must default to the invite flow
        var request = new
        {
            Groups = Array.Empty<object>(),
            Members = new[] { new { Email = email, ExternalId = "external-3" } },
            OverwriteExisting = false
        };

        var response = await _client.PostAsJsonAsync("/public/organization/import", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var organizationUsers = await organizationUserRepository.GetManyByOrganizationAsync(_organization.Id, null);

        var invitedUser = Assert.Single(organizationUsers, ou => ou.Email == email);
        Assert.Equal(OrganizationUserStatusType.Invited, invitedUser.Status);
        Assert.Equal("external-3", invitedUser.ExternalId);
    }
}
