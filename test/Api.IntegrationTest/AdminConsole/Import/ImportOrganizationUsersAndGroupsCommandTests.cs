using System.Net;
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

namespace Bit.Api.IntegrationTest.AdminConsole.Import;

public class ImportOrganizationUsersAndGroupsCommandTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private Organization _organization = null!;
    private string _ownerEmail = null!;

    public ImportOrganizationUsersAndGroupsCommandTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService((IFeatureService featureService)
            => featureService.IsEnabled(FeatureFlagKeys.ImportAsyncRefactor)
                .Returns(true));
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        // Create the owner account
        _ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        // Create the organization
        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually2023,
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
    public async Task Import_Existing_Organization_User_Succeeds()
    {
        var (email, ou) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.User);

        var externalId = Guid.NewGuid().ToString();
        var request = new OrganizationImportRequestModel();
        request.LargeImport = false;
        request.OverwriteExisting = false;
        request.Groups = [];
        request.Members = [
            new OrganizationImportRequestModel.OrganizationImportMemberRequestModel
            {
                Email = email,
                ExternalId = externalId,
                Deleted = false
            }
        ];

        var response = await _client.PostAsync($"/public/organization/import", JsonContent.Create(request));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert against the database values
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var orgUser = await organizationUserRepository.GetByIdAsync(ou.Id);

        Assert.NotNull(orgUser);
        Assert.Equal(ou.Id, orgUser.Id);
        Assert.Equal(email, orgUser.Email);
        Assert.Equal(OrganizationUserType.User, orgUser.Type);
        Assert.Equal(externalId, orgUser.ExternalId);
        Assert.Equal(OrganizationUserStatusType.Confirmed, orgUser.Status);
        Assert.Equal(_organization.Id, orgUser.OrganizationId);

    }

    [Fact]
    public async Task Import_New_Organization_User_Succeeds()
    {
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);

        var externalId = Guid.NewGuid().ToString();
        var request = new OrganizationImportRequestModel();
        request.LargeImport = false;
        request.OverwriteExisting = false;
        request.Groups = [];
        request.Members = [
            new OrganizationImportRequestModel.OrganizationImportMemberRequestModel
            {
                Email = email,
                ExternalId = externalId,
                Deleted = false
            }
        ];

        var response = await _client.PostAsync($"/public/organization/import", JsonContent.Create(request));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert against the database values
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var orgUser = await organizationUserRepository.GetByOrganizationEmailAsync(_organization.Id, email);

        Assert.NotNull(orgUser);
        Assert.Equal(email, orgUser.Email);
        Assert.Equal(OrganizationUserType.User, orgUser.Type);
        Assert.Equal(externalId, orgUser.ExternalId);
        Assert.Equal(OrganizationUserStatusType.Invited, orgUser.Status);
        Assert.Equal(_organization.Id, orgUser.OrganizationId);
    }

    [Fact]
    public async Task Import_New_And_Existing_Organization_Users_Succeeds()
    {
        // Existing organization user
        var (existingEmail, ou) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.User);
        var existingExternalId = Guid.NewGuid().ToString();

        // New organization user
        var newEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(newEmail);
        var newExternalId = Guid.NewGuid().ToString();

        var request = new OrganizationImportRequestModel();
        request.LargeImport = false;
        request.OverwriteExisting = false;
        request.Groups = [];
        request.Members = [
            new OrganizationImportRequestModel.OrganizationImportMemberRequestModel
            {
                Email = existingEmail,
                ExternalId = existingExternalId,
                Deleted = false
            },
            new OrganizationImportRequestModel.OrganizationImportMemberRequestModel
            {
                Email = newEmail,
                ExternalId = newExternalId,
                Deleted = false
            }
        ];

        var response = await _client.PostAsync($"/public/organization/import", JsonContent.Create(request));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert against the database values
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();

        // Existing user
        var existingOrgUser = await organizationUserRepository.GetByIdAsync(ou.Id);
        Assert.NotNull(existingOrgUser);
        Assert.Equal(existingEmail, existingOrgUser.Email);
        Assert.Equal(OrganizationUserType.User, existingOrgUser.Type);
        Assert.Equal(existingExternalId, existingOrgUser.ExternalId);
        Assert.Equal(OrganizationUserStatusType.Confirmed, existingOrgUser.Status);
        Assert.Equal(_organization.Id, existingOrgUser.OrganizationId);

        // New User
        var newOrgUser = await organizationUserRepository.GetByOrganizationEmailAsync(_organization.Id, newEmail);
        Assert.NotNull(newOrgUser);
        Assert.Equal(newEmail, newOrgUser.Email);
        Assert.Equal(OrganizationUserType.User, newOrgUser.Type);
        Assert.Equal(newExternalId, newOrgUser.ExternalId);
        Assert.Equal(OrganizationUserStatusType.Invited, newOrgUser.Status);
        Assert.Equal(_organization.Id, newOrgUser.OrganizationId);
    }
}
